using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.Enums;

namespace Stathub.Modules.Matches.Application.Dtos;

public sealed record MatchDto(
    Guid Id,
    Guid StageId,
    Guid HomeTeamId,
    Guid AwayTeamId,
    DateTime CreatedAtUtc,
    DateTime ScheduledTimeUtc,
    DateTime? StartedAtUtc,
    MatchStatus Status,
    int HomeScore,
    int AwayScore,
    // Снимок регламента матча: заполняется при создании из правил этапа
    int PeriodsCount,
    int PeriodDurationMinutes,
    bool ExtraTimeEnabled,
    bool PenaltyShootoutEnabled,
    // Текущий период: 0 — матч ещё не начат
    int CurrentPeriod,
    DateTime? PeriodStartedAtUtc
    )
{
    internal static MatchDto From(Match match) => new(
        match.Id,
        match.StageId,
        match.HomeTeamId,
        match.AwayTeamId,
        match.CreatedAtUtc,
        match.ScheduledTimeUtc,
        match.StartedAtUtc,
        match.Status,
        match.HomeScore,
        match.AwayScore,
        match.PeriodsCount,
        match.PeriodDurationMinutes,
        match.ExtraTimeEnabled,
        match.PenaltyShootoutEnabled,
        match.CurrentPeriod,
        match.PeriodStartedAtUtc
    );
}
