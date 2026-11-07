namespace Stathub.Modules.Rosters.Application.Interfaces;

/// <summary>Порт потребителя: существует ли игрок (реализация — поверх IPlayerReader модуля Players).</summary>
public interface IPlayerExistsProvider
{
    Task<bool> ExistsAsync(Guid playerId, CancellationToken cancellationToken = default);
}
