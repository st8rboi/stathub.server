using Stathub.Modules.Leagues.Domain.Entities;

namespace Stathub.Modules.Leagues.Application.Abstractions;

public interface ITournamentRepository
{
    Task<Tournament?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tournament>> ListByLeagueAsync(Guid leagueId, CancellationToken cancellationToken = default);

    Task AddAsync(Tournament tournament, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
