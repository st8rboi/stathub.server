using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stathub.Modules.Rosters.Application;
using Stathub.Modules.Rosters.Application.Dtos;
using Stathub.WebApi.Contracts.Rosters;
using Stathub.WebApi.Infrastructure.Auth;

namespace Stathub.WebApi.Controllers.Rosters;

/// <summary>
/// Заявки команд и игроков на этап (группа/дивизион/плей-офф): маршруты вложены в этап,
/// потому что в одном турнире бывает несколько параллельных дивизионов со своими командами.
/// </summary>
[ApiController]
[Route("stages/{stageId:guid}")]
// Читать заявки может и судья (состав нужен для автора гола) — доступ проверяет фильтр.
[Authorize(Policy = AuthPolicies.TenantMember)]
[ServiceFilter(typeof(StageRosterAccessFilter))]
public sealed class RostersController(RosterService rosterService) : ControllerBase
{
    [HttpPost("teams")]
    public async Task<IActionResult> AddTeam(
        Guid stageId,
        AddTournamentTeamRequest request,
        CancellationToken cancellationToken)
    {
        var tournamentTeamId = await rosterService.AddTeamAsync(stageId, request.TeamId, cancellationToken);

        return CreatedAtAction(
            nameof(GetTeam),
            new { stageId, id = tournamentTeamId },
            new { Id = tournamentTeamId });
    }

    [HttpGet("teams")]
    public async Task<ActionResult<IReadOnlyList<TournamentTeamDto>>> GetTeams(
        Guid stageId,
        CancellationToken cancellationToken) =>
        Ok(await rosterService.ListTeamsAsync(stageId, cancellationToken));

    [HttpGet("teams/{id:guid}")]
    public async Task<ActionResult<TournamentTeamDetailsDto>> GetTeam(
        Guid stageId,
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await rosterService.GetTeamAsync(stageId, id, cancellationToken));

    [HttpPost("teams/{id:guid}/players")]
    public async Task<ActionResult<object>> RegisterPlayer(
        Guid stageId,
        Guid id,
        RegisterPlayerRequest request,
        CancellationToken cancellationToken)
    {
        var registrationId = await rosterService.RegisterPlayerAsync(
            stageId,
            id,
            request.PlayerId,
            request.SquadNumber,
            cancellationToken);

        return Ok(new { Id = registrationId });
    }

    [HttpPost("teams/{id:guid}/players/{registrationId:guid}/release")]
    public async Task<IActionResult> ReleasePlayer(
        Guid stageId,
        Guid id,
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        await rosterService.ReleasePlayerAsync(stageId, id, registrationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("transfers")]
    public async Task<IActionResult> TransferPlayer(
        Guid stageId,
        TransferPlayerRequest request,
        CancellationToken cancellationToken)
    {
        await rosterService.TransferPlayerAsync(
            stageId,
            request.PlayerId,
            request.FromTeamId,
            request.ToTeamId,
            request.SquadNumber,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("teams/{id:guid}/withdraw")]
    public async Task<IActionResult> WithdrawTeam(
        Guid stageId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await rosterService.WithdrawTeamAsync(stageId, id, cancellationToken);
        return NoContent();
    }
}
