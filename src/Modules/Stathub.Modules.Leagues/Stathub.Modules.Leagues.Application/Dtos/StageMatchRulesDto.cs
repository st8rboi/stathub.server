namespace Stathub.Modules.Leagues.Application.Dtos;
public sealed record StageMatchRulesDto(
    int PeriodDurationMinutes,
    int PeriodsCount,
    bool ExtraTimeEnabled,
    bool PenaltyShootoutEnabled);