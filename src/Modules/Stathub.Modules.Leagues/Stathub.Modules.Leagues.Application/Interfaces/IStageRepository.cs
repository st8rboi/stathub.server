using Stathub.Modules.Leagues.Domain.Entities;

namespace Stathub.Modules.Leagues.Application.Interfaces;

public interface IStageRepository
{
    // Получить стадию по Id
    Task<Stage?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    // Добавить стадию
    Task AddAsync(
        Stage stage,
        CancellationToken cancellationToken = default);

    // Сохранить изменения
    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}