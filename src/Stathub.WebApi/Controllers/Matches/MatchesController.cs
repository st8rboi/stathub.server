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
        var matchId = await matchService.CreateMatchAsync(
            request.TournamentId,
            request.StageId,
            request.HomeTeamId,
            request.AwayTeamId,
            request.StartDate,
            request.FormatType,
            request.WinPoints,
            request.DrawPoints,
            request.LossPoints,
            request.PeriodDurationMinutes,
            request.PeriodsCount,
            cancellationToken);

        return CreatedAtAction(nameof(GetMatch), new { id = matchId }, new { id = matchId });
    }

    // Без organizerId — публичный список опубликованных лиг, с organizerId — все лиги организатора
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeagueDto>>> ListMatches(
        [FromQuery] Guid? organizerId,
        CancellationToken cancellationToken) =>
        Ok(await matchService.ListAsync(organizerId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LeagueDto>> GetLeague(Guid id, CancellationToken cancellationToken) =>
        Ok(await matchService.GetByIdAsync(id, cancellationToken));

    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<LeagueDto>> GetLeagueBySlug(string slug, CancellationToken cancellationToken) =>
        Ok(await matchService.GetBySlugAsync(slug, cancellationToken));

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> PublishLeague(Guid id, CancellationToken cancellationToken)
    {
        await matchService.PublishAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> ArchiveLeague(Guid id, CancellationToken cancellationToken)
    {
        await matchService.ArchiveAsync(id, cancellationToken);
        return NoContent();
    }
}
