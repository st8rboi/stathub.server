using Stathub.Modules.Standings.Domain.Models;

namespace Stathub.Modules.Standings.Application.Interfaces;

/// <summary>Источник результатов этапа (реализация — поверх модуля Matches).</summary>
public interface IStandingsMatchSource
{
    /// <summary>Завершённые и идущие сейчас матчи этапа (у идущих — текущий счёт).</summary>
    Task<IReadOnlyList<StandingsMatch>> GetMatchesAsync(
        Guid stageId,
        CancellationToken cancellationToken = default);
}
