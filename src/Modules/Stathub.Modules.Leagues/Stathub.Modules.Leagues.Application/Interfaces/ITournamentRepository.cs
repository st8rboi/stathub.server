using Stathub.Modules.Leagues.Domain.Entities;

namespace Stathub.Modules.Leagues.Application.Interfaces;

public interface ITournamentRepository
{
    Task<Tournament?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Турнир, которому принадлежит дивизион (со всей структурой).</summary>
    Task<Tournament?> GetByDivisionIdAsync(Guid divisionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tournament>> ListByLeagueAsync(Guid leagueId, CancellationToken cancellationToken = default);

    Task AddAsync(Tournament tournament, CancellationToken cancellationToken = default);

    /// <summary>Удаление турнира вместе с его этапами (каскад внутри модуля).</summary>
    void Remove(Tournament tournament);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
