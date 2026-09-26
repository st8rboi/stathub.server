namespace Stathub.Modules.Matches.Dtos;

public sealed record MatchFormatDto(
    int PeriodsCount,
    int PeriodDurationMinutes,
    bool ExtraTimeEnabled,
    bool PenaltyShootoutEnabled);