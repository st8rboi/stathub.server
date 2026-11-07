namespace Stathub.Modules.Players.Application.Interfaces;

using Stathub.Modules.Players.Application.Dtos;

/// <summary>
/// Проверка существования игрока для других модулей (заявки не должны ссылаться на несуществующего
/// человека). Объявлен в модуле-поставщике, реализация — в Players.Infrastructure.
/// </summary>
public interface IPlayerReader
{
    Task<bool> ExistsAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>ФИО по идентификаторам — для составов, протокола и статистики.</summary>
    Task<IReadOnlyList<PlayerNameDto>> GetNamesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}
