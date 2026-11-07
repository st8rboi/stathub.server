namespace Stathub.WebApi.Contracts.Matches;

/// <summary>
/// Батч событий матча: клиент отправляет накопленную офлайн-очередь одним запросом
/// и получает новое состояние матча.
/// </summary>
/// <param name="Events">События очереди</param>
/// <param name="SentAtUtc">Часы устройства в момент отправки: по ним сервер поправляет время событий</param>
public sealed record AppendMatchEventsRequest(
    IReadOnlyList<AppendMatchEventRequest>? Events,
    DateTime? SentAtUtc = null);
