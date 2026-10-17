namespace Stathub.Modules.Matches.Application.Dtos;

/// <summary>
/// Регламент матча, снятый с этапа. <see cref="TournamentId"/> — контекст этапа:
/// по нему матч знает свой сезон, а клиент — где искать составы команд.
/// </summary>
public sealed record MatchFormatDto(
    Guid TournamentId,
    int PeriodsCount,
    int PeriodDurationMinutes,
    bool ExtraTimeEnabled,
    bool PenaltyShootoutEnabled);
