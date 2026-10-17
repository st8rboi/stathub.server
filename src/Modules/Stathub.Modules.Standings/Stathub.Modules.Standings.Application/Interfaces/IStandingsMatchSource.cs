using Stathub.Modules.Standings.Domain.Models;

namespace Stathub.Modules.Standings.Application.Interfaces;

/// <summary>Источник результатов этапа (реализация — поверх модуля Matches).</summary>
public interface IStandingsMatchSource
{
    Task<IReadOnlyList<StandingsMatch>> GetFinishedMatchesAsync(
        Guid stageId,
        CancellationToken cancellationToken = default);
}
