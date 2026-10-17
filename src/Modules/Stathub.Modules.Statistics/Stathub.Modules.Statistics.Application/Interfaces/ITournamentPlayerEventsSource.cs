using Stathub.Modules.Statistics.Domain.Models;

namespace Stathub.Modules.Statistics.Application.Interfaces;

/// <summary>
/// События игроков турнира (реализация — поверх модуля Matches, где живёт протокол).
/// </summary>
public interface ITournamentPlayerEventsSource
{
    Task<IReadOnlyList<TournamentPlayerEvent>> GetAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);
}
