using Stathub.Modules.Finance.Application.Dtos;
using Stathub.Modules.Finance.Application.Interfaces;
using Stathub.Modules.Finance.Domain.Entities;
using Stathub.Modules.Finance.Domain.Enums;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Finance.Application;

/// <summary>
/// Взносы за матчи: тариф на турнир с переопределением на этап и начисления по каждой команде.
/// Команда платит, организатор отмечает оплату — статус, дата, способ и комментарий хранятся
/// в начислении, поэтому историю расчётов можно восстановить.
/// </summary>
public sealed class FeeService(
    IFeeSettingRepository settings,
    IMatchChargeRepository charges,
    ITeamNameSource teamNames)
{
    /// <summary>Тарифы турнира и этапа (если этап переопределяет сумму).</summary>
    public async Task<FeeSettingsDto> GetSettingsAsync(
        Guid tournamentId,
        Guid? stageId,
        CancellationToken cancellationToken = default)
    {
        var tournament = await settings.GetAsync(FeeScopeType.Tournament, tournamentId, cancellationToken);
        var stage = stageId is { } id
            ? await settings.GetAsync(FeeScopeType.Stage, id, cancellationToken)
            : null;

        var tournamentAmount = tournament?.Amount ?? 0;
        var stageAmount = stage?.Amount;
        var effective = stageAmount ?? tournamentAmount;

        return new FeeSettingsDto(tournamentAmount, stageAmount, effective);
    }

    public async Task SetTournamentFeeAsync(
        Guid tournamentId,
        int amount,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        await UpsertAsync(FeeScopeType.Tournament, tournamentId, amount, userId, cancellationToken);
    }

    /// <summary><paramref name="amount"/> = null снимает переопределение, и этап наследует тариф турнира.</summary>
    public async Task SetStageFeeAsync(
        Guid stageId,
        int? amount,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        if (amount is null)
        {
            var existing = await settings.GetAsync(FeeScopeType.Stage, stageId, cancellationToken);
            if (existing is not null)
            {
                settings.Remove(existing);
                await settings.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        await UpsertAsync(FeeScopeType.Stage, stageId, amount.Value, userId, cancellationToken);
    }

    /// <summary>
    /// Начисление на матч: две позиции (хозяева, гости) по действующему тарифу.
    /// Идемпотентно: повторный вызов для того же матча ничего не создаёт.
    /// </summary>
    public async Task ChargeMatchAsync(
        Guid matchId,
        Guid stageId,
        Guid tournamentId,
        Guid homeTeamId,
        Guid awayTeamId,
        CancellationToken cancellationToken = default)
    {
        if (await charges.AnyForMatchAsync(matchId, cancellationToken))
            return;

        var stageOverride = await settings.GetAsync(FeeScopeType.Stage, stageId, cancellationToken);
        var tournament = stageOverride is null
            ? await settings.GetAsync(FeeScopeType.Tournament, tournamentId, cancellationToken)
            : null;

        var amount = stageOverride?.Amount ?? tournament?.Amount ?? 0;
        if (amount <= 0)
            return;

        var created = new List<MatchCharge>
        {
            MatchCharge.Create(matchId, stageId, tournamentId, homeTeamId, ChargeSide.Home, amount),
            MatchCharge.Create(matchId, stageId, tournamentId, awayTeamId, ChargeSide.Away, amount)
        };

        await charges.AddRangeAsync(created, cancellationToken);
        await charges.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChargeDto>> ListStageChargesAsync(
        Guid stageId,
        bool onlyDebt,
        CancellationToken cancellationToken = default)
    {
        var list = await charges.ListByStageAsync(stageId, onlyDebt, cancellationToken);
        if (list.Count == 0)
            return [];

        var names = await teamNames.GetNamesAsync(
            list.Select(charge => charge.TeamId).Distinct().ToList(),
            cancellationToken);

        return list
            .Select(charge => ToDto(charge, names))
            .OrderBy(charge => charge.CreatedAtUtc)
            .ThenBy(charge => charge.Side)
            .ToList();
    }

    public async Task MarkPaidAsync(
        Guid chargeId,
        PaymentMethod method,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var charge = await charges.GetByIdAsync(chargeId, cancellationToken)
            ?? throw new NotFoundException("Начисление не найдено.");

        charge.MarkPaid(method, comment);
        await charges.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(
        Guid chargeId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var charge = await charges.GetByIdAsync(chargeId, cancellationToken)
            ?? throw new NotFoundException("Начисление не найдено.");

        charge.Cancel(comment);
        await charges.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Сводка по турниру или этапу: начислено, оплачено, долг.</summary>
    public async Task<FeeSummaryDto> GetSummaryAsync(
        Guid tournamentId,
        Guid? stageId,
        CancellationToken cancellationToken = default)
    {
        var list = stageId is { } id
            ? await charges.ListByStageAsync(id, onlyDebt: false, cancellationToken)
            : await charges.ListByTournamentAsync(tournamentId, cancellationToken);

        var active = list.Where(charge => charge.Status != ChargeStatus.Cancelled).ToList();
        var charged = active.Sum(charge => charge.Amount);
        var paid = active.Where(charge => charge.Status == ChargeStatus.Paid).Sum(charge => charge.Amount);

        return new FeeSummaryDto(
            charged,
            paid,
            charged - paid,
            active.Count(charge => charge.Status == ChargeStatus.Pending),
            active.Count(charge => charge.Status == ChargeStatus.Paid));
    }

    /// <summary>Долг по командам за турнир: кто сколько начислено, оплатил и остался должен.</summary>
    public async Task<IReadOnlyList<TeamDebtDto>> GetTeamDebtsAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        var list = await charges.ListByTournamentAsync(tournamentId, cancellationToken);
        var active = list.Where(charge => charge.Status != ChargeStatus.Cancelled).ToList();
        if (active.Count == 0)
            return [];

        var names = await teamNames.GetNamesAsync(
            active.Select(charge => charge.TeamId).Distinct().ToList(),
            cancellationToken);

        return active
            .GroupBy(charge => charge.TeamId)
            .Select(group =>
            {
                var charged = group.Sum(charge => charge.Amount);
                var paid = group.Where(charge => charge.Status == ChargeStatus.Paid).Sum(charge => charge.Amount);

                return new TeamDebtDto(
                    group.Key,
                    names.GetValueOrDefault(group.Key, string.Empty),
                    charged,
                    paid,
                    charged - paid);
            })
            .OrderByDescending(debt => debt.Debt)
            .ThenBy(debt => debt.TeamName)
            .ToList();
    }

    private async Task UpsertAsync(
        FeeScopeType scopeType,
        Guid scopeId,
        int amount,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var existing = await settings.GetAsync(scopeType, scopeId, cancellationToken);

        if (existing is null)
        {
            await settings.AddAsync(FeeSetting.Create(scopeType, scopeId, amount, userId), cancellationToken);
        }
        else
        {
            existing.ChangeAmount(amount, userId);
        }

        await settings.SaveChangesAsync(cancellationToken);
    }

    private static ChargeDto ToDto(MatchCharge charge, IReadOnlyDictionary<Guid, string> names) => new(
        charge.Id,
        charge.MatchId,
        charge.StageId,
        charge.TeamId,
        names.GetValueOrDefault(charge.TeamId, string.Empty),
        charge.Side,
        charge.Amount,
        charge.Status,
        charge.CreatedAtUtc,
        charge.PaidAtUtc,
        charge.PaymentMethod,
        charge.Comment);
}
