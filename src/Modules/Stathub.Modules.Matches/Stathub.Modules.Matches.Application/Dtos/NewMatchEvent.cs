using Stathub.Modules.Matches.Domain.Enums;

namespace Stathub.Modules.Matches.Application.Dtos;

/// <summary>
/// Событие, пришедшее от клиента. Id генерирует клиент (судья работает офлайн), поэтому
/// повторная отправка того же батча идемпотентна — см. <see cref="MatchEventService"/>.
/// </summary>
public sealed record NewMatchEvent(
    Guid Id,
    MatchEventType Type,
    Guid? TeamId,
    Guid? PlayerId,
    Guid? RelatedPlayerId,
    Guid? TargetEventId,
    string? Reason,
    DateTime ClientTimestampUtc,
    long SequenceNumber);
