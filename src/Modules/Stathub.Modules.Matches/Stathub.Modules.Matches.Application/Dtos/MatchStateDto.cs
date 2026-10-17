namespace Stathub.Modules.Matches.Application.Dtos;

/// <summary>
/// Полное состояние матча: сам матч (со счётом-проекцией) и протокол событий.
/// Отдаётся клиенту при открытии экрана и при переподключении — независимо от реалтайм-пуша.
/// </summary>
public sealed record MatchStateDto(
    MatchDto Match,
    IReadOnlyList<MatchEventDto> Events);
