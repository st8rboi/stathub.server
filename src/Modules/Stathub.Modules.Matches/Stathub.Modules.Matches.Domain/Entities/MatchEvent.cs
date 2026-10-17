using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Matches.Domain.Entities;

/// <summary>
/// Событие протокола матча. Протокол — append-only лог: события не редактируются и не удаляются,
/// отмена оформляется новым событием <see cref="MatchEventType.EventVoided"/> со ссылкой
/// на <see cref="TargetEventId"/>. Счёт матча — проекция этого лога (см. <see cref="Match.RebuildScoreProjection"/>).
///
/// <para>
/// Идентификатор события генерирует клиент (судья работает офлайн), поэтому повторная отправка
/// того же события идемпотентна: сервер отбрасывает события с уже известным Id.
/// </para>
/// <para>
/// Вместо универсального JSON-payload из документации здесь типизированные поля
/// (TeamId/PlayerId/RelatedPlayerId/TargetEventId/Reason): их проще валидировать и запрашивать.
/// Если появятся спорт-специфичные данные, добавим отдельную jsonb-колонку.
/// </para>
/// </summary>
public sealed class MatchEvent : Entity
{
    private const int ReasonMaxLength = 512;

    public Guid MatchId { get; private set; }
    public MatchEventType Type { get; private set; }

    /// <summary>
    /// Команда, к которой относится действие. Для <see cref="MatchEventType.OwnGoal"/> это команда
    /// игрока, забившего в свои ворота: гол при этом засчитывается сопернику.
    /// </summary>
    public Guid? TeamId { get; private set; }

    /// <summary>Основной игрок события: автор гола, получивший карточку, уходящий с поля.</summary>
    public Guid? PlayerId { get; private set; }

    /// <summary>Второй игрок события: ассистент гола или выходящий на замену.</summary>
    public Guid? RelatedPlayerId { get; private set; }

    /// <summary>Отменяемое событие. Заполняется только для <see cref="MatchEventType.EventVoided"/>.</summary>
    public Guid? TargetEventId { get; private set; }

    /// <summary>Причина отмены или пояснение к событию (необязательно).</summary>
    public string? Reason { get; private set; }

    /// <summary>Момент события по часам устройства судьи (может расходиться с серверным).</summary>
    public DateTime ClientTimestampUtc { get; private set; }

    /// <summary>Порядковый номер события внутри матча, назначается клиентом.</summary>
    public long SequenceNumber { get; private set; }

    /// <summary>Момент, когда событие принял сервер.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private MatchEvent()
    {
    }

    private MatchEvent(
        Guid id,
        Guid matchId,
        MatchEventType type,
        Guid? teamId,
        Guid? playerId,
        Guid? relatedPlayerId,
        Guid? targetEventId,
        string? reason,
        DateTime clientTimestampUtc,
        long sequenceNumber)
    {
        Id = id;
        MatchId = matchId;
        Type = type;
        TeamId = teamId;
        PlayerId = playerId;
        RelatedPlayerId = relatedPlayerId;
        TargetEventId = targetEventId;
        Reason = reason;
        ClientTimestampUtc = clientTimestampUtc;
        SequenceNumber = sequenceNumber;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static MatchEvent Create(
        Guid id,
        Guid matchId,
        MatchEventType type,
        Guid? teamId,
        Guid? playerId,
        Guid? relatedPlayerId,
        Guid? targetEventId,
        string? reason,
        DateTime clientTimestampUtc,
        long sequenceNumber)
    {
        if (id == Guid.Empty)
            throw new DomainException("Событие должно иметь идентификатор, сгенерированный клиентом (UUID).");
        if (matchId == Guid.Empty)
            throw new DomainException("MatchId обязательное поле.");
        if (clientTimestampUtc == default)
            throw new DomainException("ClientTimestampUtc обязательное поле.");
        if (sequenceNumber <= 0)
            throw new DomainException("SequenceNumber должен быть больше 0.");

        ValidateTypeRules(type, id, teamId, playerId, relatedPlayerId, targetEventId);

        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (normalizedReason is { Length: > ReasonMaxLength })
            throw new DomainException($"Причина не может быть длиннее {ReasonMaxLength} символов.");

        return new MatchEvent(
            id,
            matchId,
            type,
            teamId,
            playerId,
            relatedPlayerId,
            targetEventId,
            normalizedReason,
            clientTimestampUtc,
            sequenceNumber);
    }

    private static void ValidateTypeRules(
        MatchEventType type,
        Guid id,
        Guid? teamId,
        Guid? playerId,
        Guid? relatedPlayerId,
        Guid? targetEventId)
    {
        switch (type)
        {
            case MatchEventType.Goal:
            case MatchEventType.OwnGoal:
                if (teamId is null)
                    throw new DomainException("Гол должен быть привязан к команде.");
                if (playerId is not null && playerId == relatedPlayerId)
                    throw new DomainException("Ассистент не может совпадать с автором гола.");
                break;

            case MatchEventType.YellowCard:
            case MatchEventType.RedCard:
                if (teamId is null)
                    throw new DomainException("Карточка должна быть привязана к команде.");
                break;

            case MatchEventType.Substitution:
                if (teamId is null)
                    throw new DomainException("Замена должна быть привязана к команде.");
                if (playerId is not null && playerId == relatedPlayerId)
                    throw new DomainException("Нельзя заменить игрока на него самого.");
                break;

            case MatchEventType.EventVoided:
                if (targetEventId is null)
                    throw new DomainException("Отмена должна ссылаться на отменяемое событие.");
                if (targetEventId == id)
                    throw new DomainException("Событие не может отменять само себя.");
                break;

            default:
                throw new DomainException($"Неизвестный тип события '{type}'.");
        }
    }
}
