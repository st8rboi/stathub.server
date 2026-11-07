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
// Запись — только организатору (см. атрибуты на POST/PUT/DELETE ниже).
[Authorize(Policy = AuthPolicies.TenantMember)]
[ServiceFilter(typeof(StageRosterAccessFilter))]
public sealed class RostersController(RosterService rosterService) : ControllerBase
{
    [HttpPost("teams")]
    [Authorize(Policy = AuthPolicies.Organizer)]
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
    [Authorize(Policy = AuthPolicies.Organizer)]
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
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<IActionResult> ReleasePlayer(
        Guid stageId,
        Guid id,
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        await rosterService.ReleasePlayerAsync(stageId, id, registrationId, cancellationToken);
        return NoContent();
    }

    /// <summary>Смена игрового номера в активной заявке.</summary>
    [HttpPut("teams/{id:guid}/players/{registrationId:guid}/squad-number")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<ActionResult<PlayerRegistrationDto>> ChangeSquadNumber(
        Guid stageId,
        Guid id,
        Guid registrationId,
        ChangeSquadNumberRequest request,
        CancellationToken cancellationToken) =>
        Ok(await rosterService.ChangeSquadNumberAsync(
            stageId, id, registrationId, request.SquadNumber, cancellationToken));

    /// <summary>Удаление ошибочной заявки игрока. 409 — по ней уже есть события в матчах (отзаявите).</summary>
    [HttpDelete("teams/{id:guid}/players/{registrationId:guid}")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<IActionResult> DeleteRegistration(
        Guid stageId,
        Guid id,
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        await rosterService.DeleteRegistrationAsync(stageId, id, registrationId, cancellationToken);
        return NoContent();
    }

    /// <summary>Удаление ошибочной заявки команды. 409 — у команды есть матчи на этапе (снимите с этапа).</summary>
    [HttpDelete("teams/{id:guid}")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<IActionResult> DeleteTeam(Guid stageId, Guid id, CancellationToken cancellationToken)
    {
        await rosterService.DeleteTeamAsync(stageId, id, cancellationToken);
        return NoContent();
    }

    [HttpPost("transfers")]
    [Authorize(Policy = AuthPolicies.Organizer)]
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
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<IActionResult> WithdrawTeam(
        Guid stageId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await rosterService.WithdrawTeamAsync(stageId, id, cancellationToken);
        return NoContent();
    }
}
