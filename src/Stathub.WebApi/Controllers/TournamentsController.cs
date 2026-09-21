using Microsoft.AspNetCore.Mvc;
using Stathub.Modules.Leagues.Application;
using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.WebApi.Contracts.Leagues;

namespace Stathub.WebApi.Controllers;

[ApiController]
[Route("leagues/{leagueId:guid}/tournaments")]
public sealed class TournamentsController(TournamentService tournamentService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateTournament(
        Guid leagueId,
        CreateTournamentRequest request,
        CancellationToken cancellationToken)
    {
        var tournamentId = await tournamentService.CreateTournamentAsync(
            leagueId,
            request.Name,
            request.StartDate,
            request.EndDate,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetTournament),
            new { leagueId, tournamentId },
            new { id = tournamentId });
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TournamentSummaryDto>>> ListTournaments(
        Guid leagueId,
        CancellationToken cancellationToken) =>
        Ok(await tournamentService.ListByLeagueAsync(leagueId, cancellationToken));

    [HttpGet("{tournamentId:guid}")]
    public async Task<ActionResult<TournamentDto>> GetTournament(
        Guid leagueId,
        Guid tournamentId,
        CancellationToken cancellationToken) =>
        Ok(await tournamentService.GetByIdAsync(leagueId, tournamentId, cancellationToken));

    [HttpPost("{tournamentId:guid}/stages")]
    public async Task<IActionResult> AddStage(
        Guid leagueId,
        Guid tournamentId,
        AddStageRequest request,
        CancellationToken cancellationToken)
    {
        var stageId = await tournamentService.AddStageAsync(
            leagueId,
            tournamentId,
            request.Name,
            request.FormatType,
            request.WinPoints,
            request.DrawPoints,
            request.LossPoints,
            request.PeriodDurationMinutes,
            request.PeriodsCount,
            request.ExtraTimeEnabled,
            request.PenaltyShootoutEnabled,
            cancellationToken);

        return Created($"leagues/{leagueId}/tournaments/{tournamentId}/stages/{stageId}", new { id = stageId });
    }

    [HttpPost("{tournamentId:guid}/start")]
    public async Task<IActionResult> StartTournament(Guid leagueId, Guid tournamentId, CancellationToken cancellationToken)
    {
        await tournamentService.StartAsync(leagueId, tournamentId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{tournamentId:guid}/complete")]
    public async Task<IActionResult> CompleteTournament(Guid leagueId, Guid tournamentId, CancellationToken cancellationToken)
    {
        await tournamentService.CompleteAsync(leagueId, tournamentId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{tournamentId:guid}/cancel")]
    public async Task<IActionResult> CancelTournament(Guid leagueId, Guid tournamentId, CancellationToken cancellationToken)
    {
        await tournamentService.CancelAsync(leagueId, tournamentId, cancellationToken);
        return NoContent();
    }
}
