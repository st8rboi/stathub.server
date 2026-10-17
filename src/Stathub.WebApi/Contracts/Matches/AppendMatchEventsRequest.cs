namespace Stathub.WebApi.Contracts.Matches;

/// <summary>
/// Батч событий матча: клиент отправляет накопленную офлайн-очередь одним запросом
/// и получает новое состояние матча.
/// </summary>
public sealed record AppendMatchEventsRequest(IReadOnlyList<AppendMatchEventRequest>? Events);
