using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stathub.Modules.Identity.Application;
using Stathub.Modules.Identity.Application.Dtos;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Modules.Identity.Domain.Enums;
using Stathub.Modules.Matches.Application;
using Stathub.WebApi.Contracts.Auth;
using Stathub.WebApi.Infrastructure.Auth;

namespace Stathub.WebApi.Controllers.Accounts;

/// <summary>
/// Управление аккаунтами организации: организатор выдаёт логины судьям и тренерам
/// и назначает их на матчи/команды. Тенант всегда берётся из токена, не из запроса.
/// </summary>
[ApiController]
[Route("accounts")]
[Authorize(Policy = AuthPolicies.Organizer)]
public sealed class AccountsController(
    AccountService accountService,
    MatchService matchService,
    TenantOwnershipGuard ownershipGuard,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> ListUsers(CancellationToken cancellationToken) =>
        Ok(await accountService.ListUsersAsync(currentUser.TenantId, cancellationToken));

    [HttpPost("users")]
    public async Task<ActionResult<UserSummaryDto>> CreateUser(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var user = await accountService.CreateUserAsync(
            currentUser.TenantId,
            currentUser.UserId,
            request.Login,
            request.DisplayName,
            request.Password,
            request.Role,
            request.Email,
            cancellationToken);

        return Ok(user);
    }

    [HttpGet("assignments")]
    public async Task<ActionResult<IReadOnlyList<AssignmentSummaryDto>>> ListAssignments(
        CancellationToken cancellationToken) =>
        Ok(await accountService.ListAssignmentsAsync(currentUser.TenantId, cancellationToken));

    /// <summary>Назначение судьи на матч (ScopeType=Match) или тренера на команду (ScopeType=Team).</summary>
    [HttpPost("assignments")]
    public async Task<ActionResult<AssignmentSummaryDto>> Assign(
        AssignScopeRequest request,
        CancellationToken cancellationToken)
    {
        // Назначить судью можно только на матч своей организации: иначе, зная чужой Id,
        // организатор выдал бы доступ к протоколу чужого матча.
        if (request.ScopeType == RoleScopeType.Match && request.ScopeId is { } matchId)
        {
            var match = await matchService.GetByIdAsync(matchId, cancellationToken);
            await ownershipGuard.EnsureTournamentAsync(match.TournamentId, cancellationToken);
        }

        var assignment = await accountService.AssignScopeAsync(
            currentUser.TenantId,
            request.UserId,
            request.Role,
            request.ScopeType,
            request.ScopeId,
            currentUser.UserId,
            cancellationToken);

        return Ok(assignment);
    }

    [HttpDelete("assignments/{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        await accountService.RevokeAsync(currentUser.TenantId, id, cancellationToken);
        return NoContent();
    }
}
