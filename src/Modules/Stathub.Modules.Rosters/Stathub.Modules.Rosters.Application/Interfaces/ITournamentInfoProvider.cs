namespace Stathub.Modules.Rosters.Application.Interfaces;

/// <summary>
/// Порт потребителя: статус турнира глазами заявок. Не содержит доменных типов Leagues,
/// поэтому Application модуля Rosters остаётся зависимым только от своего домена и Shared.
/// Реализация — в Rosters.Infrastructure поверх <c>ITournamentReader</c> модуля Leagues.
/// </summary>
public interface ITournamentInfoProvider
{
    Task<TournamentRosterInfo?> GetAsync(Guid tournamentId, CancellationToken cancellationToken = default);
}

/// <param name="TournamentId">Турнир</param>
/// <param name="RosterLocked">Турнир завершён или отменён — заявки менять нельзя</param>
public sealed record TournamentRosterInfo(Guid TournamentId, bool RosterLocked);
