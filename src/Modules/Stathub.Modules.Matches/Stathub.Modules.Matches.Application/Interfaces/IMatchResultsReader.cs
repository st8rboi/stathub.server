using Stathub.Modules.Matches.Application.Dtos;

namespace Stathub.Modules.Matches.Application.Interfaces;

/// <summary>
/// Результаты завершённых матчей этапа — для расчёта турнирной таблицы.
/// Объявлен в модуле-поставщике, реализация — в Matches.Infrastructure.
/// </summary>
public interface IMatchResultsReader
{
    Task<IReadOnlyList<FinishedMatchDto>> GetFinishedByStageAsync(
        Guid stageId,
        CancellationToken cancellationToken = default);
}
