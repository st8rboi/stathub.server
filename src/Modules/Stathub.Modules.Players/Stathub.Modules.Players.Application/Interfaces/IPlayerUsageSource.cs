using Stathub.Modules.Players.Application.Dtos;

namespace Stathub.Modules.Players.Application.Interfaces;

/// <summary>
/// Где используется игрок. Объявлен здесь, а реализуется модулями-потребителями
/// (заявки, события матчей): Players не знает про них, но перед правкой или удалением
/// спрашивает все зарегистрированные источники. Между таблицами модулей нет FK, поэтому
/// это единственная защита от «висячих» ссылок.
/// </summary>
public interface IPlayerUsageSource
{
    /// <summary>Использование игрока в модуле-потребителе или null, если ссылок нет.</summary>
    Task<PlayerUsageDto?> GetUsageAsync(Guid playerId, CancellationToken cancellationToken = default);
}
