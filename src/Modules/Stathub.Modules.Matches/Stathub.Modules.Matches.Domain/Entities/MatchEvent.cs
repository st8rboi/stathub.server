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
    private const int ReviewNoteMaxLength = 256;

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

    /// <summary>
    /// Момент события с поправкой на расхождение часов устройства и сервера.
    /// У событий, принятых до появления поправки, пусто — для них берётся клиентское время.
    /// </summary>
    public DateTime? OccurredAtUtc { get; private set; }

    /// <summary>
    /// Пометка для организатора: событие принято как факт с поля, но требует разбора
    /// (например, участвовал дисквалифицированный игрок). На счёт не влияет.
    /// </summary>
    public string? ReviewNote { get; private set; }

    /// <summary>
    /// Ожидаемые голы (xG) удара — опасность момента по оценке статистика, от 0 до 1.
    /// Заполняется только у ударов (<see cref="MatchEventType.Shot"/>, <see cref="MatchEventType.ShotOnTarget"/>).
    /// </summary>
    public decimal? ExpectedGoals { get; private set; }

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
        DateTime occurredAtUtc,
        long sequenceNumber,
        decimal? expectedGoals)
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
        OccurredAtUtc = occurredAtUtc;
        SequenceNumber = sequenceNumber;
        ExpectedGoals = expectedGoals;
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
        long sequenceNumber,
        DateTime? occurredAtUtc = null,
        decimal? expectedGoals = null)
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
        var normalizedExpectedGoals = NormalizeExpectedGoals(type, expectedGoals);

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
            occurredAtUtc ?? clientTimestampUtc,
            sequenceNumber,
            normalizedExpectedGoals);
    }

    public static bool IsShotType(MatchEventType type) => type is MatchEventType.Shot or MatchEventType.ShotOnTarget;

    /// <summary>
    /// xG обязателен у удара (статистик выбирает опасность момента) и запрещён у остальных событий:
    /// у фола или гола «ожидаемых голов» нет. Храним с точностью до сотых.
    /// </summary>
    private static decimal? NormalizeExpectedGoals(MatchEventType type, decimal? expectedGoals)
    {
        if (!IsShotType(type))
        {
            if (expectedGoals is not null)
                throw new DomainException("xG указывается только у удара.");
            return null;
        }

        if (expectedGoals is not { } value)
            throw new DomainException("У удара должна быть указана опасность (xG).");
        if (value <= 0 || value > 1)
            throw new DomainException("xG удара должен быть больше 0 и не больше 1.");

        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public bool IsLifecycle => Type is MatchEventType.MatchStarted
        or MatchEventType.MatchPaused
        or MatchEventType.MatchResumed
        or MatchEventType.PeriodStarted
        or MatchEventType.MatchFinished;

    /// <summary>Событие командной статистики (удар, фол, угловой): ведёт статистик, на счёт не влияет.</summary>
    public bool IsStatistic => IsStatisticType(Type);

    public static bool IsStatisticType(MatchEventType type) => type is MatchEventType.Shot
        or MatchEventType.ShotOnTarget
        or MatchEventType.Foul
        or MatchEventType.Corner;

    public DateTime EffectiveTimeUtc => OccurredAtUtc ?? ClientTimestampUtc;

    public void MarkForReview(string note)
    {
        ReviewNote = note.Length > ReviewNoteMaxLength ? note[..ReviewNoteMaxLength] : note;
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

            case MatchEventType.Shot:
            case MatchEventType.ShotOnTarget:
            case MatchEventType.Foul:
            case MatchEventType.Corner:
                if (teamId is null)
                    throw new DomainException("Событие статистики должно быть привязано к команде.");
                break;

            case MatchEventType.EventVoided:
                if (targetEventId is null)
                    throw new DomainException("Отмена должна ссылаться на отменяемое событие.");
                if (targetEventId == id)
                    throw new DomainException("Событие не может отменять само себя.");
                break;

            case MatchEventType.MatchStarted:
            case MatchEventType.MatchPaused:
            case MatchEventType.MatchResumed:
            case MatchEventType.PeriodStarted:
            case MatchEventType.MatchFinished:
                break;

            default:
                throw new DomainException($"Неизвестный тип события '{type}'.");
        }
    }
}
