using Stathub.Modules.Teams.Application.Dtos;

namespace Stathub.Modules.Teams.Application.Interfaces;

/// <summary>
/// Чтение команд потребителями (заявки, таблицы, публичная витрина): проверка существования
/// и названия по идентификаторам. Объявлен в модуле-поставщике, реализация — в Teams.Infrastructure.
/// </summary>
public interface ITeamReader
{
    Task<bool> ExistsAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TeamNameDto>> GetNamesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}
