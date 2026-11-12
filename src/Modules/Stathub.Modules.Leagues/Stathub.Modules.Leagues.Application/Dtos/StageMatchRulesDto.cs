namespace Stathub.Modules.Leagues.Application.Dtos;

/// <summary>
/// Правила матча, снятые с этапа. <see cref="TournamentId"/> и <see cref="DivisionId"/> — не правила,
/// а контекст этапа: они нужны потребителям (матчи, заявки), чтобы знать, в каком сезоне и
/// дивизионе проходит матч. <see cref="Groups"/> — группы этапа: по ним матч определяет свою группу.
/// </summary>
public sealed record StageMatchRulesDto(
    Guid TournamentId,
    Guid DivisionId,
    int PeriodDurationMinutes,
    int PeriodsCount,
    bool ExtraTimeEnabled,
    bool PenaltyShootoutEnabled,
    IReadOnlyList<StageGroupTeamsDto> Groups);

/// <summary>Группа этапа и её команды — без доменных типов Leagues.</summary>
public sealed record StageGroupTeamsDto(Guid GroupId, string Name, int Order, IReadOnlyList<Guid> TeamIds);
