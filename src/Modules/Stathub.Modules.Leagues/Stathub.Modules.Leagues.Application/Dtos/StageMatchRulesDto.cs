namespace Stathub.Modules.Leagues.Application.Dtos;

/// <summary>
/// Правила матча, снятые с этапа. <see cref="TournamentId"/> — не правило, а контекст этапа:
/// он нужен потребителям (матчи, заявки), чтобы знать, в каком сезоне проходит матч.
/// </summary>
public sealed record StageMatchRulesDto(
    Guid TournamentId,
    int PeriodDurationMinutes,
    int PeriodsCount,
    bool ExtraTimeEnabled,
    bool PenaltyShootoutEnabled);
