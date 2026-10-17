using Stathub.Modules.Matches.Domain.Entities;

namespace Stathub.Modules.Matches.Application.Interfaces;

public interface IMatchEventRepository
{
    /// <summary>Весь протокол матча в порядке, назначенном клиентом (SequenceNumber).</summary>
    Task<IReadOnlyList<MatchEvent>> GetByMatchIdAsync(Guid matchId, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyList<MatchEvent> events, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
