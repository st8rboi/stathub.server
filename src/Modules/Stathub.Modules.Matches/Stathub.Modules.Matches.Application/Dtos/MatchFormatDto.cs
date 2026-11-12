namespace Stathub.Modules.Matches.Application.Dtos;

/// <summary>
/// Регламент матча, снятый с этапа. <see cref="TournamentId"/> и <see cref="DivisionId"/> — контекст этапа:
/// по ним матч знает свой сезон и дивизион, а клиент — где искать составы команд.
/// <see cref="Groups"/> — группы этапа: по ним матч определяет свою группу.
/// </summary>
public sealed record MatchFormatDto(
    Guid TournamentId,
    Guid DivisionId,
    int PeriodsCount,
    int PeriodDurationMinutes,
    bool ExtraTimeEnabled,
    bool PenaltyShootoutEnabled,
    IReadOnlyList<MatchGroupDto> Groups);

/// <summary>Группа этапа и её команды.</summary>
public sealed record MatchGroupDto(Guid GroupId, IReadOnlyList<Guid> TeamIds);
