using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.Enums;

namespace Stathub.Modules.Matches.Application.Dtos;

public sealed record MatchDto(
    Guid Id,
    Guid StageId,
    Guid TournamentId,
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
    DateTime? PeriodStartedAtUtc,
    // Секундомер: часы периода = (PausedAtUtc ?? сейчас) - PeriodStartedAtUtc - PeriodPausedMilliseconds
    DateTime? PausedAtUtc,
    long PeriodPausedMilliseconds,
    DateTime? FinishedAtUtc,
    // Сколько событий протокола ждут разбора организатором
    int EventsForReview
    )
{
    internal static MatchDto From(Match match) => new(
        match.Id,
        match.StageId,
        match.TournamentId,
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
        match.PeriodStartedAtUtc,
        match.PausedAtUtc,
        match.PeriodPausedMilliseconds,
        match.FinishedAtUtc,
        match.EventsForReview
    );
}
