using Stathub.Modules.Leagues.Domain.Entities;
using Stathub.Modules.Leagues.Domain.Enums;

namespace Stathub.Modules.Leagues.Application.Abstractions;

public interface ILeagueRepository
{
    Task<League?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<League?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<League>> ListAsync(
        Guid? organizerId,
        LeagueStatus? status,
        CancellationToken cancellationToken = default);

    Task AddAsync(League league, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
