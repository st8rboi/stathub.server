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
/// судья и статистик — только назначенные им. Проверка на каждый запрос (см. MatchAccessGuard).
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
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<IActionResult> CreateMatch(CreateMatchRequest request, CancellationToken cancellationToken)
    {
        // Новый матч создаётся только в рамках своего этапа.
        await matchAccessGuard.EnsureStageAsync(request.StageId, cancellationToken);

        // На этапе играют только команды, заявленные в его дивизион: иначе в протокол
        // попадёт команда из другого дивизиона того же турнира.
        var divisionId = await rosterService.GetDivisionIdByStageAsync(request.StageId, cancellationToken);

        if (!await rosterService.IsTeamRegisteredAsync(divisionId, request.HomeTeamId, cancellationToken))
            throw new DomainException("Хозяева не заявлены в этот дивизион.");

        if (!await rosterService.IsTeamRegisteredAsync(divisionId, request.AwayTeamId, cancellationToken))
            throw new DomainException("Гости не заявлены в этот дивизион.");

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
    /// Матчи этапа: организатор видит весь этап своей лиги, судья и статистик — только назначенные матчи.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> GetMatches(
        [FromQuery] Guid stageId,
        CancellationToken cancellationToken)
    {
        var matches = await matchService.ListByStageAsync(stageId, cancellationToken);

        if (currentUser.Role is UserRole.Referee or UserRole.Statistician)
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

    /// <summary>
    /// Перенос матча и замена команд (только не начатый матч без событий).
    /// Неоплаченные взносы переходят к новым командам; оплаченный взнос блокирует замену (409).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<ActionResult<MatchDto>> UpdateMatch(
        Guid id,
        UpdateMatchRequest request,
        CancellationToken cancellationToken)
    {
        await matchAccessGuard.EnsureMatchAsync(id, cancellationToken);
        var current = await matchService.GetByIdAsync(id, cancellationToken);

        // Те же правила, что при создании: играют только команды, заявленные в дивизион матча.
        if (!await rosterService.IsTeamRegisteredAsync(current.DivisionId, request.HomeTeamId, cancellationToken))
            throw new DomainException("Хозяева не заявлены в этот дивизион.");

        if (!await rosterService.IsTeamRegisteredAsync(current.DivisionId, request.AwayTeamId, cancellationToken))
            throw new DomainException("Гости не заявлены в этот дивизион.");

        // Сначала все проверки, затем изменения: у матча и взносов разные БД-контексты.
        await feeService.EnsureMatchTeamsCanChangeAsync(id, request.HomeTeamId, request.AwayTeamId, cancellationToken);

        var match = await matchService.UpdateAsync(
            id,
            request.StartTimeUtc,
            request.HomeTeamId,
            request.AwayTeamId,
            cancellationToken);

        await feeService.ReassignMatchTeamsAsync(id, match.HomeTeamId, match.AwayTeamId, cancellationToken);
        await matchNotifier.PublishMatchChangedAsync(id, cancellationToken);

        return Ok(match);
    }

    /// <summary>
    /// Удаление ошибочно созданного матча (не начатого, без событий) вместе с его взносами.
    /// Назначения судей на матч отзываются. 409 — матч уже вели или по нему есть оплаченный взнос.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<IActionResult> DeleteMatch(Guid id, CancellationToken cancellationToken)
    {
        await matchAccessGuard.EnsureMatchAsync(id, cancellationToken);

        // Сначала все проверки, затем изменения: у матча, взносов и назначений разные БД-контексты.
        await matchService.EnsureCanDeleteAsync(id, cancellationToken);
        await feeService.EnsureMatchChargesRemovableAsync(id, cancellationToken);

        await matchService.DeleteAsync(id, cancellationToken);
        await feeService.RemoveMatchChargesAsync(id, cancellationToken);
        await accountService.RevokeMatchAssignmentsAsync(currentUser.TenantId, id, cancellationToken);

        return NoContent();
    }

    // Батч событий протокола, включая смену статуса матча. Идемпотентно по клиентскому UUID:
    // повторы из офлайн-очереди не создают дублей и возвращаются в duplicateEventIds.
    // Статистик тем же эндпоинтом отправляет удары, фолы и угловые — остальное ему отклоняется.
    [HttpPost("{id:guid}/events")]
    public async Task<ActionResult<AppendEventsResultDto>> AppendEvents(
        Guid id,
        AppendMatchEventsRequest request,
        CancellationToken cancellationToken)
    {
        var access = await matchAccessGuard.EnsureMatchAsync(id, cancellationToken);

        var events = (request.Events ?? [])
            .Select(ToNewEvent)
            .ToList();

        var result = await matchEventService.AppendEventsAsync(
            id,
            events,
            request.SentAtUtc,
            statisticsOnly: access == MatchAccess.StatisticsOnly,
            cancellationToken);

        // Пуш зрителям — только после коммита; сам пуш best-effort (doc §4, §9).
        await matchNotifier.PublishMatchChangedAsync(id, cancellationToken);

        return Ok(result);
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
        request.SequenceNumber,
        request.ExpectedGoals);
}
