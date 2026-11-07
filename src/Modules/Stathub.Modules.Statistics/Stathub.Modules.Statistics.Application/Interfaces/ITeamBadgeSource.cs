using Stathub.Modules.Statistics.Application.Dtos;

namespace Stathub.Modules.Statistics.Application.Interfaces;

/// <summary>Команды (имя и логотип) для строк статистики (реализация — поверх модуля Teams).</summary>
public interface ITeamBadgeSource
{
    Task<IReadOnlyDictionary<Guid, TeamBadgeDto>> GetAsync(
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken = default);
}
