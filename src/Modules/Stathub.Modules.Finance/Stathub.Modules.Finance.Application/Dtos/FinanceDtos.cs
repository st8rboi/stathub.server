using Stathub.Modules.Finance.Domain.Enums;

namespace Stathub.Modules.Finance.Application.Dtos;

/// <summary>Тарифы взносов: турнирный и переопределение этапа (null — не задано).</summary>
public sealed record FeeSettingsDto(
    int TournamentAmount,
    int? StageAmount,
    int EffectiveAmount);

public sealed record ChargeDto(
    Guid Id,
    Guid? MatchId,
    Guid? StageId,
    Guid TeamId,
    string TeamName,
    ChargeSide? Side,
    ChargeKind Kind,
    string? Description,
    int Amount,
    ChargeStatus Status,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    PaymentMethod? PaymentMethod,
    string? Comment);

/// <summary>Сводка по турниру или этапу; <see cref="ByKind"/> — та же сводка в разрезе видов начислений.</summary>
public sealed record FeeSummaryDto(
    int Charged,
    int Paid,
    int Debt,
    int PendingCount,
    int PaidCount,
    IReadOnlyList<ChargeKindSummaryDto> ByKind);

/// <summary>Итоги по одному виду начислений (только виды, по которым что-то начислено).</summary>
public sealed record ChargeKindSummaryDto(
    ChargeKind Kind,
    int Charged,
    int Paid,
    int Debt);

/// <summary>Ручное начисление команде: вступительный взнос, штраф или прочее.</summary>
public sealed record AddManualChargeDto(
    Guid TeamId,
    Guid? StageId,
    ChargeKind Kind,
    int Amount,
    string? Description);

/// <summary>Долг одной команды за турнир.</summary>
public sealed record TeamDebtDto(
    Guid TeamId,
    string TeamName,
    int Charged,
    int Paid,
    int Debt);
