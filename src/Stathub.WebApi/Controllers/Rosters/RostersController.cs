using Microsoft.AspNetCore.Mvc;
using Stathub.Modules.Rosters.Application;
using Stathub.Modules.Rosters.Application.Dtos;
using Stathub.WebApi.Contracts.Rosters;

namespace Stathub.WebApi.Controllers.Rosters;

/// <summary>
/// Заявки команд и игроков на турнир: маршруты вложены в турнир, потому что заявка
/// существует только в его рамках.
/// </summary>
[ApiController]
[Route("tournaments/{tournamentId:guid}")]
public sealed class RostersController(RosterService rosterService) : ControllerBase
{
    [HttpPost("teams")]
    public async Task<IActionResult> AddTeam(
        Guid tournamentId,
        AddTournamentTeamRequest request,
        CancellationToken cancellationToken)
    {
        var tournamentTeamId = await rosterService.AddTeamAsync(tournamentId, request.TeamId, cancellationToken);

        return CreatedAtAction(
            nameof(GetTeam),
            new { tournamentId, id = tournamentTeamId },
            new { Id = tournamentTeamId });
    }

    [HttpGet("teams")]
    public async Task<ActionResult<IReadOnlyList<TournamentTeamDto>>> GetTeams(
        Guid tournamentId,
        CancellationToken cancellationToken) =>
        Ok(await rosterService.ListTeamsAsync(tournamentId, cancellationToken));

    [HttpGet("teams/{id:guid}")]
    public async Task<ActionResult<TournamentTeamDetailsDto>> GetTeam(
        Guid tournamentId,
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await rosterService.GetTeamAsync(tournamentId, id, cancellationToken));

    [HttpPost("teams/{id:guid}/players")]
    public async Task<ActionResult<object>> RegisterPlayer(
        Guid tournamentId,
        Guid id,
        RegisterPlayerRequest request,
        CancellationToken cancellationToken)
    {
        var registrationId = await rosterService.RegisterPlayerAsync(
            tournamentId,
            id,
            request.PlayerId,
            request.SquadNumber,
            cancellationToken);

        return Ok(new { Id = registrationId });
    }

    [HttpPost("teams/{id:guid}/players/{registrationId:guid}/release")]
    public async Task<IActionResult> ReleasePlayer(
        Guid tournamentId,
        Guid id,
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        await rosterService.ReleasePlayerAsync(tournamentId, id, registrationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("transfers")]
    public async Task<IActionResult> TransferPlayer(
        Guid tournamentId,
        TransferPlayerRequest request,
        CancellationToken cancellationToken)
    {
        await rosterService.TransferPlayerAsync(
            tournamentId,
            request.PlayerId,
            request.FromTeamId,
            request.ToTeamId,
            request.SquadNumber,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("teams/{id:guid}/withdraw")]
    public async Task<IActionResult> WithdrawTeam(
        Guid tournamentId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await rosterService.WithdrawTeamAsync(tournamentId, id, cancellationToken);
        return NoContent();
    }
}
