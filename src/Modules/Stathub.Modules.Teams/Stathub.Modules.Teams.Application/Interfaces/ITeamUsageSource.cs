using Stathub.Modules.Teams.Application.Dtos;

namespace Stathub.Modules.Teams.Application.Interfaces;

/// <summary>
/// Где используется команда. Объявлен здесь, а реализуется модулями-потребителями
/// (заявки, матчи): Teams не знает про них, но перед правкой или удалением спрашивает
/// все зарегистрированные источники. Между таблицами модулей нет FK, поэтому это
/// единственная защита от «висячих» ссылок.
/// </summary>
public interface ITeamUsageSource
{
    /// <summary>Использование команды в модуле-потребителе или null, если ссылок нет.</summary>
    Task<TeamUsageDto?> GetUsageAsync(Guid teamId, CancellationToken cancellationToken = default);
}
