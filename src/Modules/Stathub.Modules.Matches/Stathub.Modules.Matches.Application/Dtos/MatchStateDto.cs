namespace Stathub.Modules.Matches.Application.Dtos;

/// <summary>
/// Полное состояние матча: сам матч (со счётом-проекцией), протокол событий и командная
/// статистика (проекция того же протокола; <see cref="HasStatistics"/> — отмечено ли хоть что-то).
/// Отдаётся клиенту при открытии экрана и при переподключении — независимо от реалтайм-пуша.
/// </summary>
public sealed record MatchStateDto(
    MatchDto Match,
    IReadOnlyList<MatchEventDto> Events,
    MatchStatisticsDto Statistics,
    bool HasStatistics);
