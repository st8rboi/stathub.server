using Microsoft.AspNetCore.Mvc;
using Stathub.Modules.Matches.Application;
using Stathub.Modules.Matches.Application.Dtos;
using Stathub.WebApi.Contracts.Matches;
using Stathub.WebApi.Infrastructure.Public;

namespace Stathub.WebApi.Controllers.Matches;

[ApiController]
[Route("matches")]
public sealed class MatchesController(
    MatchService matchService,
    MatchEventService matchEventService,
    MatchNotifier matchNotifier) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateMatch(CreateMatchRequest request, CancellationToken cancellationToken)
    {
        var match = await matchService.CreateMatchAsync(
            request.StageId,
            request.HomeTeamId,
            request.AwayTeamId,
            request.StartTimeUtc,
            cancellationToken);
        
        return CreatedAtAction(
            nameof(GetMatchById),
            new { id = match },
            new { Id = match });
    }

    // Список матчей этапа: по нему судейский клиент выбирает матч для подключения.
    // Пагинации нет — на объёме одного этапа любительской лиги она не нужна.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MatchDto>>> GetMatches(
        [FromQuery] Guid stageId,
        CancellationToken cancellationToken) =>
        Ok(await matchService.ListByStageAsync(stageId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MatchDto>> GetMatchById(Guid id, CancellationToken cancellationToken) =>
        Ok(await matchService.GetByIdAsync(id, cancellationToken));

    // Полное состояние матча: счёт + протокол. Независимый путь синхронизации на случай
    // пропущенного realtime-обновления (документация, раздел 9).
    [HttpGet("{id:guid}/state")]
    public async Task<ActionResult<MatchStateDto>> GetMatchState(Guid id, CancellationToken cancellationToken) =>
        Ok(await matchEventService.GetStateAsync(id, cancellationToken));

    // Батч событий протокола. Идемпотентно по клиентскому UUID: повторы из офлайн-очереди
    // не создают дублей и возвращаются в duplicateEventIds.
    [HttpPost("{id:guid}/events")]
    public async Task<ActionResult<AppendEventsResultDto>> AppendEvents(
        Guid id,
        AppendMatchEventsRequest request,
        CancellationToken cancellationToken)
    {
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
        await matchService.StartMatchAsync(id, cancellationToken);
        await matchNotifier.PublishMatchChangedAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/stop")]
    public async Task<IActionResult> StopMatch(Guid id, CancellationToken cancellationToken)
    {
        await matchService.StopMatchAsync(id, cancellationToken);
        await matchNotifier.PublishMatchChangedAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> ResumeMatch(Guid id, CancellationToken cancellationToken)
    {
        await matchService.ResumeMatchAsync(id, cancellationToken);
        await matchNotifier.PublishMatchChangedAsync(id, cancellationToken);
        return NoContent();
    }

    // Отдельное действие судьи, а не следствие resume: пауза бывает и внутри периода
    [HttpPost("{id:guid}/next-period")]
    public async Task<IActionResult> StartNextPeriod(Guid id, CancellationToken cancellationToken)
    {
        await matchService.StartNextPeriodAsync(id, cancellationToken);
        await matchNotifier.PublishMatchChangedAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/finish")]
    public async Task<IActionResult> FinishMatch(Guid id, CancellationToken cancellationToken)
    {
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
