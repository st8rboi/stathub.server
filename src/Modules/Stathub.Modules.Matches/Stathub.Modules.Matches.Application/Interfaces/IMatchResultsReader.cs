using Stathub.Modules.Matches.Application.Dtos;

namespace Stathub.Modules.Matches.Application.Interfaces;

/// <summary>
/// Результаты матчей этапа — для расчёта турнирной таблицы.
/// Объявлен в модуле-поставщике, реализация — в Matches.Infrastructure.
/// </summary>
public interface IMatchResultsReader
{
    /// <summary>
    /// Завершённые матчи этапа и идущие сейчас (с текущим счётом, <see cref="StageMatchResultDto.IsLive"/>):
    /// по ним строится live-таблица.
    /// </summary>
    Task<IReadOnlyList<StageMatchResultDto>> GetResultsByStageAsync(
        Guid stageId,
        CancellationToken cancellationToken = default);
}
