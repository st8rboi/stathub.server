namespace Stathub.Modules.Matches.Application.Dtos;

/// <summary>
/// Результат приёма батча: какие события записаны, какие оказались повторами (идемпотентность
/// по клиентскому UUID) и новое состояние матча — клиенту не нужен второй запрос.
/// </summary>
public sealed record AppendEventsResultDto(
    IReadOnlyList<Guid> AcceptedEventIds,
    IReadOnlyList<Guid> DuplicateEventIds,
    MatchStateDto State);
