namespace Stathub.Modules.Matches.Application.Interfaces;

/// <summary>
/// Проверка допуска игрока к матчу. Реализуется адаптером поверх дисциплинарного модуля и заявок,
/// поэтому Matches не знает ни о дисквалификациях, ни о составах, ни о чужих доменах.
/// </summary>
public interface IPlayerEligibility
{
    /// <summary>
    /// Дисквалификация игрока в турнире. Бан следует за игроком: после трансфера он действует
    /// и в новой команде, поэтому команда события здесь не участвует.
    /// </summary>
    /// <returns>Сколько матчей осталось дисквалификации; null — игрок допущен.</returns>
    Task<int?> GetBanLeftAsync(
        Guid tournamentId,
        Guid playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Был ли игрок заявлен за команду в турнире на указанную дату (заявка открыта не позже
    /// и закрыта позже этой даты: в день отзаявки или трансфера за старую команду он уже не играет).
    /// Несуществующий игрок, игрок соперника, отзаявленный или перешедший — не заявлен.
    /// </summary>
    Task<bool> IsRegisteredAsync(
        Guid tournamentId,
        Guid teamId,
        Guid playerId,
        DateOnly onDate,
        CancellationToken cancellationToken = default);
}
