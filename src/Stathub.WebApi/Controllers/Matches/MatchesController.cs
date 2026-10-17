using Microsoft.AspNetCore.Mvc;
using Stathub.Modules.Matches.Application;
using Stathub.Modules.Matches.Application.Dtos;
using Stathub.WebApi.Contracts.Matches;

namespace Stathub.WebApi.Controllers.Matches;

[ApiController]
[Route("matches")]
public sealed class MatchesController(MatchService matchService) : ControllerBase
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

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> StartMatch(Guid id, CancellationToken cancellationToken)
    {
        await matchService.StartMatchAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/stop")]
    public async Task<IActionResult> StopMatch(Guid id, CancellationToken cancellationToken)
    {
        await matchService.StopMatchAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> ResumeMatch(Guid id, CancellationToken cancellationToken)
    {
        await matchService.ResumeMatchAsync(id, cancellationToken);
        return NoContent();
    }

    // Отдельное действие судьи, а не следствие resume: пауза бывает и внутри периода
    [HttpPost("{id:guid}/next-period")]
    public async Task<IActionResult> StartNextPeriod(Guid id, CancellationToken cancellationToken)
    {
        await matchService.StartNextPeriodAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/finish")]
    public async Task<IActionResult> FinishMatch(Guid id, CancellationToken cancellationToken)
    {
        await matchService.FinishMatchAsync(id, cancellationToken);
        return NoContent();
    }
}
