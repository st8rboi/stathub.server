using Stathub.Modules.Finance.Domain.Enums;

namespace Stathub.Modules.Finance.Application.Dtos;

/// <summary>Тарифы взносов: турнирный и переопределение этапа (null — не задано).</summary>
public sealed record FeeSettingsDto(
    int TournamentAmount,
    int? StageAmount,
    int EffectiveAmount);

public sealed record ChargeDto(
    Guid Id,
    Guid MatchId,
    Guid StageId,
    Guid TeamId,
    string TeamName,
    ChargeSide Side,
    int Amount,
    ChargeStatus Status,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    PaymentMethod? PaymentMethod,
    string? Comment);

/// <summary>Сводка по турниру или этапу.</summary>
public sealed record FeeSummaryDto(
    int Charged,
    int Paid,
    int Debt,
    int PendingCount,
    int PaidCount);

/// <summary>Долг одной команды за турнир.</summary>
public sealed record TeamDebtDto(
    Guid TeamId,
    string TeamName,
    int Charged,
    int Paid,
    int Debt);
