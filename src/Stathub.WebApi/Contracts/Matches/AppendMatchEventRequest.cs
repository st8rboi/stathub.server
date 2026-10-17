using Stathub.Modules.Matches.Domain.Enums;

namespace Stathub.WebApi.Contracts.Matches;

/// <summary>
/// Одно событие в батче. <paramref name="Id"/> обязателен и генерируется клиентом:
/// по нему сервер отбрасывает повторы при повторной отправке офлайн-очереди.
/// </summary>
/// <param name="Id">Клиентский UUID события</param>
/// <param name="Type">Тип события протокола</param>
/// <param name="TeamId">Команда действия (для автогола — команда игрока, забившего в свои ворота)</param>
/// <param name="PlayerId">Основной игрок: автор гола, получивший карточку, уходящий с поля</param>
/// <param name="RelatedPlayerId">Второй игрок: ассистент гола или выходящий на замену</param>
/// <param name="TargetEventId">Отменяемое событие — только для типа EventVoided</param>
/// <param name="Reason">Причина отмены или пояснение</param>
/// <param name="ClientTimestampUtc">Момент события по часам устройства судьи</param>
/// <param name="SequenceNumber">Порядок события внутри матча (назначает клиент)</param>
public sealed record AppendMatchEventRequest(
    Guid Id,
    MatchEventType Type,
    Guid? TeamId,
    Guid? PlayerId,
    Guid? RelatedPlayerId,
    Guid? TargetEventId,
    string? Reason,
    DateTime ClientTimestampUtc,
    long SequenceNumber);
