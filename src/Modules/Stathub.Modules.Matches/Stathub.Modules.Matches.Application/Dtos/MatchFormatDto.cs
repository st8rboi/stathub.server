namespace Stathub.Modules.Matches.Application.Dtos;

public sealed record MatchFormatDto(
    int PeriodsCount,
    int PeriodDurationMinutes,
    bool ExtraTimeEnabled,
    bool PenaltyShootoutEnabled);