using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Leagues.Domain.Entities;

namespace Stathub.Modules.Leagues.Infrastructure.Persistence;

internal sealed class TournamentRepository(LeaguesDbContext dbContext) : ITournamentRepository
{
    public Task<Tournament?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Tournaments
            .Include(t => t.Stages)
            .ThenInclude(s => s.TiebreakerRules)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Tournament>> ListByLeagueAsync(
        Guid leagueId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Tournaments
            .AsNoTracking()
            .Where(t => t.LeagueId == leagueId)
            .OrderByDescending(t => t.StartDate)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Tournament tournament, CancellationToken cancellationToken = default) =>
        await dbContext.Tournaments.AddAsync(tournament, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
