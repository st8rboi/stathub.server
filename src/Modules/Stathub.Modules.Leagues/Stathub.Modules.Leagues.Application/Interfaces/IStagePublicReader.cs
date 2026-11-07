using Stathub.Modules.Leagues.Application.Dtos;

namespace Stathub.Modules.Leagues.Application.Interfaces;

/// <summary>
/// Чтение этапа вместе с турниром и лигой — для публичной витрины и расчёта таблицы.
/// Объявлен в модуле-поставщике, реализация — в Leagues.Infrastructure.
/// </summary>
public interface IStagePublicReader
{
    Task<StagePublicInfoDto?> GetAsync(Guid stageId, CancellationToken cancellationToken = default);
}
