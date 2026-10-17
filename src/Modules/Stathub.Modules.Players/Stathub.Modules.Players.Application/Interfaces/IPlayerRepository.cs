using Stathub.Modules.Players.Domain.Entities;

namespace Stathub.Modules.Players.Application.Interfaces;

public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Подсказки существующих игроков для typeahead-поиска при добавлении в заявку.
    /// Поиск устойчив к небольшим опечаткам (триграммы) и ограничен <paramref name="limit"/>.
    /// Решение «это тот самый игрок или новый» всегда принимает человек — автоматического
    /// слияния по совпадению ФИО нет (documentation/doc.md §9.3).
    /// </summary>
    Task<IReadOnlyList<Player>> FindByNameAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Недавно добавленные игроки — стартовый список на странице админки.</summary>
    Task<IReadOnlyList<Player>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Игроки по набору идентификаторов: нужны потребителям (заявки), чтобы показать состав,
    /// не зная ничего о таблице игроков.
    /// </summary>
    Task<IReadOnlyList<Player>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    Task AddAsync(Player player, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
