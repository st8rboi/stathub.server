using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Matches.Application.Interfaces;
using Stathub.Modules.Matches.Domain.Entities;

namespace Stathub.Modules.Matches.Infrastructure.Persistence;

internal class MatchEventRepository(MatchesDbContext dbContext) : IMatchEventRepository
{
    public async Task<IReadOnlyList<MatchEvent>> GetByMatchIdAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        // Протокол append-only: события только читаются, поэтому отслеживание не нужно
        // и не конфликтует с AddRange в том же контексте.
        return await dbContext.MatchEvents
            .AsNoTracking()
            .Where(matchEvent => matchEvent.MatchId == matchId)
            .OrderBy(matchEvent => matchEvent.SequenceNumber)
            .ThenBy(matchEvent => matchEvent.ClientTimestampUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(
        IReadOnlyList<MatchEvent> events,
        CancellationToken cancellationToken = default)
    {
        await dbContext.MatchEvents.AddRangeAsync(events, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Тот же scoped DbContext, что и у MatchRepository, поэтому сохраняются
        // и события, и пересчитанная проекция счёта в матче.
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
