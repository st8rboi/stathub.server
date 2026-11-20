using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

    public async Task<IReadOnlySet<Guid>> FindExistingIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return new HashSet<Guid>();

        var existing = await dbContext.MatchEvents
            .AsNoTracking()
            .Where(matchEvent => ids.Contains(matchEvent.Id))
            .Select(matchEvent => matchEvent.Id)
            .ToListAsync(cancellationToken);

        return existing.ToHashSet();
    }

    public async Task<IMatchWriteScope> BeginMatchWriteAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Транзакционная advisory-блокировка по Id матча: снимается сама при commit/rollback,
            // не требует схемы и не мешает чтению матча витриной.
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({matchId.ToString()}, {0L}))",
                cancellationToken);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }

        // Всё, что контекст успел прочитать до блокировки (например, матч при проверке доступа),
        // могло устареть: следующий батч мог закоммититься, пока мы ждали.
        dbContext.ChangeTracker.Clear();

        return new MatchWriteScope(transaction);
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

    private sealed class MatchWriteScope(IDbContextTransaction transaction) : IMatchWriteScope
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
