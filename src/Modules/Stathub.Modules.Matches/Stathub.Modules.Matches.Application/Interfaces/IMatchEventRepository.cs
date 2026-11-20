using Stathub.Modules.Matches.Domain.Entities;

namespace Stathub.Modules.Matches.Application.Interfaces;

public interface IMatchEventRepository
{
    /// <summary>Весь протокол матча в порядке, назначенном клиентом (SequenceNumber).</summary>
    Task<IReadOnlyList<MatchEvent>> GetByMatchIdAsync(Guid matchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Какие из идентификаторов уже заняты событиями — в любом матче. Id события генерирует клиент,
    /// поэтому совпадение с событием другого матча возможно (ошибка клиента) и должно давать
    /// отказ этому событию, а не ошибку записи всего батча.
    /// </summary>
    Task<IReadOnlySet<Guid>> FindExistingIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Открывает транзакцию записи протокола и берёт блокировку матча до её завершения.
    /// Батчи одного матча (судья, статистик, организатор, повторная отправка) применяются
    /// строго по очереди: иначе параллельные батчи читают протокол без чужих событий и
    /// перезаписывают счёт друг друга. Матч и протокол после вызова нужно читать заново.
    /// </summary>
    Task<IMatchWriteScope> BeginMatchWriteAsync(Guid matchId, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyList<MatchEvent> events, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Транзакция записи протокола: без <see cref="CommitAsync"/> изменения откатываются.</summary>
public interface IMatchWriteScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
