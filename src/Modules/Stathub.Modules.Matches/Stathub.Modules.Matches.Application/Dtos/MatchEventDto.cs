using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.Enums;

namespace Stathub.Modules.Matches.Application.Dtos;

/// <summary>
/// Событие протокола в ответе API. <see cref="IsVoided"/> считается по логу:
/// отменённые события остаются в протоколе, но помечаются — в UI их показывают зачёркнутыми.
/// </summary>
public sealed record MatchEventDto(
    Guid Id,
    Guid MatchId,
    MatchEventType Type,
    Guid? TeamId,
    Guid? PlayerId,
    Guid? RelatedPlayerId,
    Guid? TargetEventId,
    string? Reason,
    DateTime ClientTimestampUtc,
    long SequenceNumber,
    DateTime CreatedAtUtc,
    bool IsVoided)
{
    internal static MatchEventDto From(MatchEvent matchEvent, IReadOnlySet<Guid> voidedEventIds) => new(
        matchEvent.Id,
        matchEvent.MatchId,
        matchEvent.Type,
        matchEvent.TeamId,
        matchEvent.PlayerId,
        matchEvent.RelatedPlayerId,
        matchEvent.TargetEventId,
        matchEvent.Reason,
        matchEvent.ClientTimestampUtc,
        matchEvent.SequenceNumber,
        matchEvent.CreatedAtUtc,
        voidedEventIds.Contains(matchEvent.Id));
}
