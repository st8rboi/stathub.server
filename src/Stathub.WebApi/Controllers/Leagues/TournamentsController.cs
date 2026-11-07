using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stathub.Modules.Leagues.Application;
using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.WebApi.Contracts.Leagues;
using Stathub.WebApi.Infrastructure.Auth;

namespace Stathub.WebApi.Controllers.Leagues;

[ApiController]
[Route("leagues/{leagueId:guid}/tournaments")]
[Authorize(Policy = AuthPolicies.Organizer)]
[ServiceFilter(typeof(OwnedLeagueFilter))]
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

    [HttpPut("{tournamentId:guid}")]
    public async Task<ActionResult<TournamentDto>> UpdateTournament(
        Guid leagueId,
        Guid tournamentId,
        UpdateTournamentRequest request,
        CancellationToken cancellationToken) =>
        Ok(await tournamentService.UpdateAsync(
            leagueId, tournamentId, request.Name, request.StartDate, request.EndDate, cancellationToken));

    /// <summary>Удаление турнира с этапами. 409 — в турнире есть заявки или матчи (его можно отменить).</summary>
    [HttpDelete("{tournamentId:guid}")]
    public async Task<IActionResult> DeleteTournament(Guid leagueId, Guid tournamentId, CancellationToken cancellationToken)
    {
        await tournamentService.DeleteAsync(leagueId, tournamentId, cancellationToken);
        return NoContent();
    }

    /// <summary>Изменение этапа. 409 — смена правил после старта турнира или при наличии матчей.</summary>
    [HttpPut("{tournamentId:guid}/stages/{stageId:guid}")]
    public async Task<ActionResult<StageDto>> UpdateStage(
        Guid leagueId,
        Guid tournamentId,
        Guid stageId,
        UpdateStageRequest request,
        CancellationToken cancellationToken) =>
        Ok(await tournamentService.UpdateStageAsync(
            leagueId,
            tournamentId,
            stageId,
            request.Name,
            request.FormatType,
            request.WinPoints,
            request.DrawPoints,
            request.LossPoints,
            request.PeriodDurationMinutes,
            request.PeriodsCount,
            request.ExtraTimeEnabled,
            request.PenaltyShootoutEnabled,
            cancellationToken));

    /// <summary>Удаление этапа. 409 — в этапе есть заявки или матчи.</summary>
    [HttpDelete("{tournamentId:guid}/stages/{stageId:guid}")]
    public async Task<IActionResult> DeleteStage(
        Guid leagueId,
        Guid tournamentId,
        Guid stageId,
        CancellationToken cancellationToken)
    {
        await tournamentService.DeleteStageAsync(leagueId, tournamentId, stageId, cancellationToken);
        return NoContent();
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
