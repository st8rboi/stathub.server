using Stathub.Modules.Statistics.Domain.Models;

namespace Stathub.Modules.Statistics.Application.Interfaces;

/// <summary>
/// События игроков дивизиона (реализация — поверх модуля Matches, где живёт протокол).
/// </summary>
public interface ITournamentPlayerEventsSource
{
    Task<IReadOnlyList<TournamentPlayerEvent>> GetByDivisionAsync(
        Guid divisionId,
        CancellationToken cancellationToken = default);
}
