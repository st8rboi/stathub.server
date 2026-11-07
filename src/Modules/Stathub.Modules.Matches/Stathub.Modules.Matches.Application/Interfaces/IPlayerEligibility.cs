namespace Stathub.Modules.Matches.Application.Interfaces;

/// <summary>
/// Проверка допуска игрока к матчу. Реализуется адаптером поверх дисциплинарного модуля,
/// поэтому Matches не знает ни о дисквалификациях, ни о чужих доменах.
/// </summary>
public interface IPlayerEligibility
{
    /// <returns>Сколько матчей осталось дисквалификации; null — игрок допущен.</returns>
    Task<int?> GetBanLeftAsync(
        Guid tournamentId,
        Guid teamId,
        Guid playerId,
        CancellationToken cancellationToken = default);
}
