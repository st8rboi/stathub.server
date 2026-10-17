using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Matches.Application.Interfaces;
using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.Enums;

namespace Stathub.Modules.Matches.Infrastructure.Persistence;

internal class MatchRepository(MatchesDbContext dbContext) : IMatchRepository
{
    public async Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Matches.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IReadOnlyList<Match>> GetByStageIdAsync(Guid stageId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Matches
            .Where(m => m.StageId == stageId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Match>> GetByStatusAsync(
        IReadOnlyCollection<MatchStatus> statuses,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Matches
            .AsNoTracking()
            .Where(match => statuses.Contains(match.Status))
            .OrderBy(match => match.ScheduledTimeUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Match>> GetByPeriodAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Matches
            .AsNoTracking()
            .Where(match => match.ScheduledTimeUtc >= fromUtc && match.ScheduledTimeUtc < toUtc)
            .OrderBy(match => match.ScheduledTimeUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Match match, CancellationToken cancellationToken = default)
    {
        await dbContext.Matches.AddAsync(match, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}