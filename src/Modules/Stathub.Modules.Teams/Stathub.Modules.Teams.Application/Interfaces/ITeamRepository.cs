using Stathub.Modules.Teams.Domain.Entities;

namespace Stathub.Modules.Teams.Application.Interfaces;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Загрузка для изменения: чтение отслеживается EF, иначе SaveChanges не увидит правку.</summary>
    Task<Team?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Список команд по алфавиту. <paramref name="query"/> — необязательный фильтр по названию
    /// (регистронезависимый поиск подстроки), нужен для выпадающих списков в админке.
    /// </summary>
    Task<IReadOnlyList<Team>> ListAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken = default);

    Task AddAsync(Team team, CancellationToken cancellationToken = default);

    /// <summary>Команды по набору идентификаторов — для отображения названий в матчах и составах.</summary>
    Task<IReadOnlyList<Team>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
