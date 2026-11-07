using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stathub.Modules.Finance.Application;
using Stathub.Modules.Finance.Application.Dtos;
using Stathub.Modules.Finance.Domain.Enums;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.WebApi.Infrastructure.Auth;

namespace Stathub.WebApi.Controllers.Finance;

public sealed record SetFeeRequest(int Amount);

public sealed record SetStageFeeRequest(int? Amount);

public sealed record PayChargeRequest(PaymentMethod Method, string? Comment);

public sealed record CancelChargeRequest(string? Comment);

/// <summary>
/// Взносы за матчи: тариф на турнир/этап, начисления по командам и отметка оплаты.
/// Всё под политикой организатора и проверкой владения турниром/этапом.
/// </summary>
[ApiController]
[Route("finance")]
[Authorize(Policy = AuthPolicies.Organizer)]
public sealed class FinanceController(
    FeeService feeService,
    TenantOwnershipGuard ownershipGuard,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("tournaments/{tournamentId:guid}/settings")]
    [ServiceFilter(typeof(OwnedTournamentFilter))]
    public async Task<ActionResult<FeeSettingsDto>> GetSettings(
        Guid tournamentId,
        [FromQuery] Guid? stageId,
        CancellationToken cancellationToken) =>
        Ok(await feeService.GetSettingsAsync(tournamentId, stageId, cancellationToken));

    [HttpPut("tournaments/{tournamentId:guid}/fee")]
    [ServiceFilter(typeof(OwnedTournamentFilter))]
    public async Task<IActionResult> SetTournamentFee(
        Guid tournamentId,
        SetFeeRequest request,
        CancellationToken cancellationToken)
    {
        await feeService.SetTournamentFeeAsync(
            tournamentId,
            request.Amount,
            currentUser.UserId,
            cancellationToken);

        return NoContent();
    }

    /// <summary>Сумма для этапа; <c>amount = null</c> снимает переопределение.</summary>
    [HttpPut("stages/{stageId:guid}/fee")]
    [ServiceFilter(typeof(OwnedStageFilter))]
    public async Task<IActionResult> SetStageFee(
        Guid stageId,
        SetStageFeeRequest request,
        CancellationToken cancellationToken)
    {
        await feeService.SetStageFeeAsync(stageId, request.Amount, currentUser.UserId, cancellationToken);
        return NoContent();
    }

    [HttpGet("stages/{stageId:guid}/charges")]
    [ServiceFilter(typeof(OwnedStageFilter))]
    public async Task<ActionResult<IReadOnlyList<ChargeDto>>> ListCharges(
        Guid stageId,
        [FromQuery] bool onlyDebt,
        CancellationToken cancellationToken) =>
        Ok(await feeService.ListStageChargesAsync(stageId, onlyDebt, cancellationToken));

    [HttpPost("charges/{id:guid}/pay")]
    public async Task<IActionResult> Pay(
        Guid id,
        PayChargeRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureOwnedChargeAsync(id, cancellationToken);
        await feeService.MarkPaidAsync(id, request.Method, request.Comment, cancellationToken);
        return NoContent();
    }

    [HttpPost("charges/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        CancelChargeRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureOwnedChargeAsync(id, cancellationToken);
        await feeService.CancelAsync(id, request.Comment, cancellationToken);
        return NoContent();
    }

    [HttpGet("tournaments/{tournamentId:guid}/summary")]
    [ServiceFilter(typeof(OwnedTournamentFilter))]
    public async Task<ActionResult<FeeSummaryDto>> GetSummary(
        Guid tournamentId,
        [FromQuery] Guid? stageId,
        CancellationToken cancellationToken) =>
        Ok(await feeService.GetSummaryAsync(tournamentId, stageId, cancellationToken));

    [HttpGet("tournaments/{tournamentId:guid}/team-debts")]
    [ServiceFilter(typeof(OwnedTournamentFilter))]
    public async Task<ActionResult<IReadOnlyList<TeamDebtDto>>> GetTeamDebts(
        Guid tournamentId,
        CancellationToken cancellationToken) =>
        Ok(await feeService.GetTeamDebtsAsync(tournamentId, cancellationToken));

    private async Task EnsureOwnedChargeAsync(Guid chargeId, CancellationToken cancellationToken)
    {
        var tournamentId = await feeService.GetChargeTournamentIdAsync(chargeId, cancellationToken);
        await ownershipGuard.EnsureTournamentAsync(tournamentId, cancellationToken);
    }
}
