using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Modules.Rosters.Application;
using Stathub.Modules.Rosters.Application.Dtos;
using Stathub.WebApi.Contracts.Rosters;
using Stathub.WebApi.Infrastructure.Auth;

namespace Stathub.WebApi.Controllers.Rosters;

/// <summary>
/// Заявки команд и игроков в дивизион: маршруты вложены в дивизион, потому что в одном турнире
/// бывает несколько параллельных дивизионов со своими командами, а состав один на все этапы дивизиона.
/// </summary>
[ApiController]
[Route("divisions/{divisionId:guid}")]
// Читать заявки может и судья (состав нужен для автора гола) — доступ проверяет фильтр.
// Запись — только организатору (см. атрибуты на POST/PUT/DELETE ниже).
[Authorize(Policy = AuthPolicies.TenantMember)]
[ServiceFilter(typeof(DivisionRosterAccessFilter))]
public sealed class RostersController(RosterService rosterService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Заявка команды. С <c>copyFromTournamentTeamId</c> — сразу с составом из другой заявки команды;
    /// в ответе — сколько игроков скопировано и кто пропущен (уже заявлен за другую команду турнира).
    /// </summary>
    [HttpPost("teams")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<ActionResult<AddTournamentTeamResult>> AddTeam(
        Guid divisionId,
        AddTournamentTeamRequest request,
        CancellationToken cancellationToken)
    {
        var result = await rosterService.AddTeamAsync(
            divisionId,
            request.TeamId,
            request.CopyFromTournamentTeamId,
            currentUser.TenantId,
            cancellationToken);

        return CreatedAtAction(nameof(GetTeam), new { divisionId, id = result.Id }, result);
    }

    /// <summary>Заявки команды в других турнирах, из которых можно скопировать состав.</summary>
    [HttpGet("roster-sources")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<ActionResult<IReadOnlyList<RosterSourceDto>>> GetRosterSources(
        Guid divisionId,
        [FromQuery] Guid teamId,
        CancellationToken cancellationToken) =>
        Ok(await rosterService.ListRosterSourcesAsync(divisionId, teamId, currentUser.TenantId, cancellationToken));

    [HttpGet("teams")]
    public async Task<ActionResult<IReadOnlyList<TournamentTeamDto>>> GetTeams(
        Guid divisionId,
        CancellationToken cancellationToken) =>
        Ok(await rosterService.ListTeamsAsync(divisionId, cancellationToken));

    [HttpGet("teams/{id:guid}")]
    public async Task<ActionResult<TournamentTeamDetailsDto>> GetTeam(
        Guid divisionId,
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await rosterService.GetTeamAsync(divisionId, id, cancellationToken));

    [HttpPost("teams/{id:guid}/players")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<ActionResult<object>> RegisterPlayer(
        Guid divisionId,
        Guid id,
        RegisterPlayerRequest request,
        CancellationToken cancellationToken)
    {
        var registrationId = await rosterService.RegisterPlayerAsync(
            divisionId,
            id,
            request.PlayerId,
            cancellationToken);

        return Ok(new { Id = registrationId });
    }

    [HttpPost("teams/{id:guid}/players/{registrationId:guid}/release")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<IActionResult> ReleasePlayer(
        Guid divisionId,
        Guid id,
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        await rosterService.ReleasePlayerAsync(divisionId, id, registrationId, cancellationToken);
        return NoContent();
    }

    /// <summary>Удаление ошибочной заявки игрока. 409 — по ней уже есть события в матчах (отзаявите).</summary>
    [HttpDelete("teams/{id:guid}/players/{registrationId:guid}")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<IActionResult> DeleteRegistration(
        Guid divisionId,
        Guid id,
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        await rosterService.DeleteRegistrationAsync(divisionId, id, registrationId, cancellationToken);
        return NoContent();
    }

    /// <summary>Удаление ошибочной заявки команды. 409 — у команды есть матчи в дивизионе (снимите с турнира).</summary>
    [HttpDelete("teams/{id:guid}")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<IActionResult> DeleteTeam(Guid divisionId, Guid id, CancellationToken cancellationToken)
    {
        await rosterService.DeleteTeamAsync(divisionId, id, cancellationToken);
        return NoContent();
    }

    /// <summary>Трансфер внутри турнира; команды могут быть из разных дивизионов.</summary>
    [HttpPost("transfers")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<IActionResult> TransferPlayer(
        Guid divisionId,
        TransferPlayerRequest request,
        CancellationToken cancellationToken)
    {
        await rosterService.TransferPlayerAsync(
            divisionId,
            request.PlayerId,
            request.FromTeamId,
            request.ToTeamId,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("teams/{id:guid}/withdraw")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<IActionResult> WithdrawTeam(
        Guid divisionId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await rosterService.WithdrawTeamAsync(divisionId, id, cancellationToken);
        return NoContent();
    }
}

/// <summary>
/// Составы глазами этапа — только чтение. Матч знает свой этап, поэтому судья и экран матчей
/// ищут составы по нему; сами заявки принадлежат дивизиону этапа (править — через
/// <c>/divisions/{divisionId}/...</c>).
/// </summary>
[ApiController]
[Route("stages/{stageId:guid}")]
[Authorize(Policy = AuthPolicies.TenantMember)]
[ServiceFilter(typeof(StageRosterAccessFilter))]
public sealed class StageRostersController(RosterService rosterService) : ControllerBase
{
    [HttpGet("teams")]
    public async Task<ActionResult<IReadOnlyList<TournamentTeamDto>>> GetTeams(
        Guid stageId,
        CancellationToken cancellationToken)
    {
        var divisionId = await rosterService.GetDivisionIdByStageAsync(stageId, cancellationToken);
        return Ok(await rosterService.ListTeamsAsync(divisionId, cancellationToken));
    }

    [HttpGet("teams/{id:guid}")]
    public async Task<ActionResult<TournamentTeamDetailsDto>> GetTeam(
        Guid stageId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var divisionId = await rosterService.GetDivisionIdByStageAsync(stageId, cancellationToken);
        return Ok(await rosterService.GetTeamAsync(divisionId, id, cancellationToken));
    }
}
