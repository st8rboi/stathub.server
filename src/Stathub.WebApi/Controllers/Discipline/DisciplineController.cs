using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stathub.Modules.Discipline.Application;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.WebApi.Infrastructure.Auth;

namespace Stathub.WebApi.Controllers.Discipline;

public sealed record IssueDisqualificationRequest(
    Guid TeamId,
    Guid PlayerId,
    string Reason,
    int? MatchesBanned,
    string? Comment);

public sealed record LiftDisqualificationRequest(string? Comment);

/// <summary>
/// Дисквалификации в пределах турнира: выдача и снятие — организатор,
/// список доступен и судье (для пометок в протоколе).
/// </summary>
[ApiController]
[Route("discipline")]
[Authorize(Policy = AuthPolicies.TenantMember)]
public sealed class DisciplineController(
    DisciplineService disciplineService,
    Stathub.Modules.Leagues.Application.Interfaces.IStagePublicReader stageReader,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("tournaments/{tournamentId:guid}")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    [ServiceFilter(typeof(OwnedTournamentFilter))]
    public async Task<ActionResult<IReadOnlyList<DisqualificationDto>>> List(
        Guid tournamentId,
        CancellationToken cancellationToken) =>
        Ok(await disciplineService.ListAsync(tournamentId, cancellationToken));

    [HttpPost("tournaments/{tournamentId:guid}")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    [ServiceFilter(typeof(OwnedTournamentFilter))]
    public async Task<IActionResult> Issue(
        Guid tournamentId,
        IssueDisqualificationRequest request,
        CancellationToken cancellationToken)
    {
        var id = await disciplineService.IssueAsync(
            tournamentId,
            request.TeamId,
            request.PlayerId,
            request.Reason,
            request.MatchesBanned,
            currentUser.UserId,
            request.Comment,
            cancellationToken);

        return Ok(new { id });
    }

    [HttpPost("disqualifications/{id:guid}/lift")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<IActionResult> Lift(
        Guid id,
        LiftDisqualificationRequest request,
        CancellationToken cancellationToken)
    {
        await disciplineService.LiftAsync(id, request.Comment, cancellationToken);
        return NoContent();
    }

    /// <summary>Действующие дисквалификации этапа — для пометок в протоколе судьи.</summary>
    [HttpGet("stages/{stageId:guid}")]
    [ServiceFilter(typeof(StageRosterAccessFilter))]
    public async Task<ActionResult<IReadOnlyList<DisqualificationDto>>> ListForStage(
        Guid stageId,
        CancellationToken cancellationToken)
    {
        var stage = await stageReader.GetAsync(stageId, cancellationToken)
            ?? throw new Stathub.Shared.Exceptions.NotFoundException($"Этап '{stageId}' не найден.");

        var list = await disciplineService.ListAsync(stage.TournamentId, cancellationToken);

        return Ok(list.Where(item => item.IsActive).ToList());
    }
}
