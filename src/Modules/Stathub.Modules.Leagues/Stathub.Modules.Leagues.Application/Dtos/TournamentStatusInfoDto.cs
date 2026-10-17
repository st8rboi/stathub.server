namespace Stathub.Modules.Leagues.Application.Dtos;

/// <summary>
/// Минимум сведений о турнире, нужный другим модулям (например, заявкам): без доменных типов
/// Leagues, чтобы потребитель не ссылался на чужой домен.
/// </summary>
/// <param name="TournamentId">Турнир</param>
/// <param name="LeagueId">Лига турнира</param>
/// <param name="RosterLocked">Завершённый или отменённый турнир — менять заявки нельзя</param>
public sealed record TournamentStatusInfoDto(
    Guid TournamentId,
    Guid LeagueId,
    bool RosterLocked);
