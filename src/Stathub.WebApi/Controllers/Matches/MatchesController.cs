using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stathub.Modules.Identity.Application;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Modules.Identity.Domain.Enums;
using Stathub.Modules.Matches.Application;
using Stathub.Modules.Matches.Application.Dtos;
using Stathub.Modules.Rosters.Application;
using Stathub.Modules.Finance.Application;
using Stathub.Shared.Exceptions;
using Stathub.WebApi.Contracts.Matches;
using Stathub.WebApi.Infrastructure.Auth;
using Stathub.WebApi.Infrastructure.Public;

namespace Stathub.WebApi.Controllers.Matches;

/// <summary>
/// Матчи и протокол. Доступ: организатор/админ — матчи своей организации,
/// судья — только назначенные ему. Проверка на каждый запрос (см. MatchAccessGuard).
/// </summary>
[ApiController]
[Route("matches")]
[Authorize(Policy = AuthPolicies.TenantMember)]
public sealed class MatchesController(
    MatchService matchService,
    MatchEventService matchEventService,
    MatchNotifier matchNotifier,
    MatchAccessGuard matchAccessGuard,
    AccountService accountService,
    RosterService rosterService,
    FeeService feeService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateMatch(CreateMatchRequest request, CancellationToken cancellationToken)
    {
        // Новый матч создаётся только в рамках своего этапа.
        await matchAccessGuard.EnsureStageAsync(request.StageId, cancellationToken);

        // В дивизионе играют только заявленные на этот этап команды: иначе в протокол
        // попадёт команда из другого дивизиона того же турнира.
        if (!await rosterService.IsTeamRegisteredAsync(request.StageId, request.HomeTeamId, cancellationToken))
            throw new DomainException("Хозяева не заявлены на этот этап.");

        if (!await rosterService.IsTeamRegisteredAsync(request.StageId, request.AwayTeamId, cancellationToken))
            throw new DomainException("Гости не заявлены на этот этап.");

        var match = await matchService.CreateMatchAsync(
            request.StageId,
            request.HomeTeamId,
            request.AwayTeamId,
            request.StartTimeUtc,
            cancellationToken);

        // Взнос начисляется сразу по действующему тарифу этапа/турнира (0 — взносов нет).
        var created = await matchService.GetByIdAsync(match, cancellationToken);
        await feeService.ChargeMatchAsync(
            created.Id,
            created.StageId,
            created.TournamentId,
            created.HomeTeamId,
            created.AwayTeamId,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetMatchById),
            new { id = match },
            new { Id = match });
    }

    /// <summary>
    /// Матчи этапа: организатор видит весь этап своей лиги, судья — только назначенные матчи.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> GetMatches(
        [FromQuery] Guid stageId,
        CancellationToken cancellationToken)
    {
        var matches = await matchService.ListByStageAsync(stageId, cancellationToken);

        if (currentUser.Role is UserRole.Referee)
        {
            var assigned = await accountService.GetAssignedMatchIdsAsync(
                currentUser.UserId,
                currentUser.TenantId,
                cancellationToken);

            return Ok(matches.Where(match => assigned.Contains(match.Id)).ToList());
        }

        await matchAccessGuard.EnsureStageAsync(stageId, cancellationToken);
        return Ok(matches);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MatchDto>> GetMatchById(Guid id, CancellationToken cancellationToken)
    {
        await matchAccessGuard.EnsureMatchAsync(id, cancellationToken);
        return Ok(await matchService.GetByIdAsync(id, cancellationToken));
    }

    // Полное состояние матча: счёт + протокол. Независимый путь синхронизации на случай
    // пропущенного realtime-обновления (документация, раздел 9).
    [HttpGet("{id:guid}/state")]
    public async Task<ActionResult<MatchStateDto>> GetMatchState(Guid id, CancellationToken cancellationToken)
    {
        await matchAccessGuard.EnsureMatchAsync(id, cancellationToken);
        return Ok(await matchEventService.GetStateAsync(id, cancellationToken));
    }

    // Батч событий протокола. Идемпотентно по клиентскому UUID: повторы из офлайн-очереди
    // не создают дублей и возвращаются в duplicateEventIds.
    [HttpPost("{id:guid}/events")]
    public async Task<ActionResult<AppendEventsResultDto>> AppendEvents(
        Guid id,
        AppendMatchEventsRequest request,
        CancellationToken cancellationToken)
    {
        await matchAccessGuard.EnsureMatchAsync(id, cancellationToken);

        var events = (request.Events ?? [])
            .Select(ToNewEvent)
            .ToList();

        var result = await matchEventService.AppendEventsAsync(id, events, cancellationToken);

        // Пуш зрителям — только после коммита; сам пуш best-effort (doc §4, §9).
        await matchNotifier.PublishMatchChangedAsync(id, cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> StartMatch(Guid id, CancellationToken cancellationToken)
    {
        await matchAccessGuard.EnsureMatchAsync(id, cancellationToken);
        await matchService.StartMatchAsync(id, cancellationToken);
        await matchNotifier.PublishMatchChangedAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/stop")]
    public async Task<IActionResult> StopMatch(Guid id, CancellationToken cancellationToken)
    {
        await matchAccessGuard.EnsureMatchAsync(id, cancellationToken);
        await matchService.StopMatchAsync(id, cancellationToken);
        await matchNotifier.PublishMatchChangedAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> ResumeMatch(Guid id, CancellationToken cancellationToken)
    {
        await matchAccessGuard.EnsureMatchAsync(id, cancellationToken);
        await matchService.ResumeMatchAsync(id, cancellationToken);
        await matchNotifier.PublishMatchChangedAsync(id, cancellationToken);
        return NoContent();
    }

    // Отдельное действие судьи, а не следствие resume: пауза бывает и внутри периода
    [HttpPost("{id:guid}/next-period")]
    public async Task<IActionResult> StartNextPeriod(Guid id, CancellationToken cancellationToken)
    {
        await matchAccessGuard.EnsureMatchAsync(id, cancellationToken);
        await matchService.StartNextPeriodAsync(id, cancellationToken);
        await matchNotifier.PublishMatchChangedAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/finish")]
    public async Task<IActionResult> FinishMatch(Guid id, CancellationToken cancellationToken)
    {
        await matchAccessGuard.EnsureMatchAsync(id, cancellationToken);
        await matchService.FinishMatchAsync(id, cancellationToken);
        await matchNotifier.PublishMatchChangedAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Единственное место, где HTTP-контракт превращается в модель Application:
    /// контроллер не валидирует события — это делает домен.
    /// </summary>
    private static NewMatchEvent ToNewEvent(AppendMatchEventRequest request) => new(
        request.Id,
        request.Type,
        request.TeamId,
        request.PlayerId,
        request.RelatedPlayerId,
        request.TargetEventId,
        request.Reason,
        request.ClientTimestampUtc,
        request.SequenceNumber);
}
