using Stathub.Modules.Standings.Application.Dtos;

namespace Stathub.Modules.Standings.Application.Interfaces;

/// <summary>Команды для строк таблицы (реализация — поверх модуля Teams).</summary>
public interface IStandingsTeamNameSource
{
    Task<IReadOnlyDictionary<Guid, StandingsTeamDto>> GetTeamsAsync(
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken = default);
}
