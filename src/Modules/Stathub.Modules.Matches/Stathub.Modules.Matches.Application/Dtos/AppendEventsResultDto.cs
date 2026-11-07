namespace Stathub.Modules.Matches.Application.Dtos;

/// <summary>
/// Результат приёма батча: какие события записаны, какие оказались повторами (идемпотентность
/// по клиентскому UUID), какие отклонены и новое состояние матча — клиенту не нужен второй запрос.
/// </summary>
public sealed record AppendEventsResultDto(
    IReadOnlyList<Guid> AcceptedEventIds,
    IReadOnlyList<Guid> DuplicateEventIds,
    IReadOnlyList<RejectedEventDto> RejectedEvents,
    MatchStateDto State);

/// <summary>
/// Событие, которое технически нельзя принять (чужая команда, битые данные, закрытый протокол).
/// Отклоняется только оно: остальная очередь судьи продолжает отправляться.
/// </summary>
public sealed record RejectedEventDto(Guid EventId, string Reason);
