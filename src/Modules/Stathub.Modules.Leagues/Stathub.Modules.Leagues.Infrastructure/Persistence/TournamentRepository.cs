using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Leagues.Domain.Entities;

namespace Stathub.Modules.Leagues.Infrastructure.Persistence;

internal sealed class TournamentRepository(LeaguesDbContext dbContext) : ITournamentRepository
{
    public Task<Tournament?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        WithStructure().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<Tournament?> GetByDivisionIdAsync(Guid divisionId, CancellationToken cancellationToken = default) =>
        WithStructure().FirstOrDefaultAsync(
            t => dbContext.Divisions.Any(d => d.Id == divisionId && d.TournamentId == t.Id),
            cancellationToken);

    // Турнир всегда грузится целиком: дивизионы, этапы, тай-брейки и группы — один агрегат.
    private IQueryable<Tournament> WithStructure() =>
        dbContext.Tournaments
            .Include(t => t.Divisions)
            .Include(t => t.Stages).ThenInclude(s => s.TiebreakerRules)
            .Include(t => t.Stages).ThenInclude(s => s.Groups)
            .AsSplitQuery();

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

    public void Remove(Tournament tournament) => dbContext.Tournaments.Remove(tournament);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
