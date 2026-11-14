using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Modules.Matches.Domain.ValueObjects;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Matches.Domain.Entities;

public sealed class Match : AggregateRoot
{
    /// <summary>
    /// Короткий сквозной номер матча (1, 2, 3…) для людей — например, ссылки на трансляционный
    /// оверлей. Выдаёт база при вставке, до сохранения равен 0. Везде в API ключ — по-прежнему Id.
    /// </summary>
    public int Number { get; private set; }

    public Guid StageId { get; private set; } // Стадия (этап) турнира
    public Guid TournamentId { get; private set; } // Турнир (сезон) этапа — контекст заявок и составов
    public Guid DivisionId { get; private set; } // Дивизион этапа — граница заявок и статистики игроков
    public Guid? GroupId { get; private set; } // Группа кругового этапа; null — этап без групп
    public Guid HomeTeamId { get; private set; } // Первая команда
    public Guid AwayTeamId { get; private set; } // Вторая команда

    public DateTime CreatedAtUtc { get; private set; } // Создан в
    public DateTime ScheduledTimeUtc { get; private set; } // Запланирован на
    public DateTime? StartedAtUtc { get; private set; } // Реально начался в

    public MatchStatus Status { get; private set; } // Завершен, Запланирован, В процессе, Отменен

    public int HomeScore { get; private set; } // Счет первой команды
    public int AwayScore { get; private set; } // Счет второй команды

    // Снимок правил этапа на момент создания матча.
    // Читается из Stage.MatchFormatRule один раз, чтобы правка правил этапа
    // не меняла уже созданные и сыгранные матчи.
    public int PeriodsCount { get; private set; }
    public int PeriodDurationMinutes { get; private set; }
    public bool ExtraTimeEnabled { get; private set; }
    public bool PenaltyShootoutEnabled { get; private set; }

    public int CurrentPeriod { get; private set; } // Текущий период; 0 — матч ещё не начат
    public DateTime? PeriodStartedAtUtc { get; private set; } // Когда начался текущий период
    public DateTime? PausedAtUtc { get; private set; } // Когда остановлена игра; null — часы идут
    public long PeriodPausedMilliseconds { get; private set; } // Сумма завершённых пауз текущего периода
    public DateTime? FinishedAtUtc { get; private set; } // Финальный свисток

    public int EventsForReview { get; private set; } // Неотменённые события с пометкой для организатора

    private Match()
    {
    }

    private Match(
        Guid id,
        Guid stageId,
        Guid tournamentId,
        Guid divisionId,
        Guid? groupId,
        Guid homeTeamId,
        Guid awayTeamId,
        DateTime scheduledTimeUtc,
        MatchFormat format)
    {
        Id = id;
        StageId = stageId;
        TournamentId = tournamentId;
        DivisionId = divisionId;
        GroupId = groupId;
        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
        ScheduledTimeUtc = scheduledTimeUtc;
        CreatedAtUtc = DateTime.UtcNow;

        Status = MatchStatus.Scheduled;
        HomeScore = 0;
        AwayScore = 0;

        PeriodsCount = format.PeriodsCount;
        PeriodDurationMinutes = format.PeriodDurationMinutes;
        ExtraTimeEnabled = format.ExtraTimeEnabled;
        PenaltyShootoutEnabled = format.PenaltyShootoutEnabled;

        CurrentPeriod = 0;
        PeriodStartedAtUtc = null;
    }

    public static Match Create(
        Guid stageId,
        Guid tournamentId,
        Guid divisionId,
        Guid homeTeamId,
        Guid awayTeamId,
        DateTime scheduledTimeUtc,
        MatchFormat format,
        Guid? groupId = null)
    {
        if (stageId == Guid.Empty)
            throw new DomainException("StageId обязательное поле.");
        if (tournamentId == Guid.Empty)
            throw new DomainException("TournamentId обязательное поле.");
        if (divisionId == Guid.Empty)
            throw new DomainException("DivisionId обязательное поле.");
        if (groupId == Guid.Empty)
            throw new DomainException("GroupId указан некорректно.");
        if (homeTeamId == awayTeamId)
            throw new DomainException("Команда не может играть сама с собой.");
        if (homeTeamId == Guid.Empty)
            throw new DomainException("HomeTeamId обязательное поле.");
        if (awayTeamId == Guid.Empty)
            throw new DomainException("AwayTeamId обязательное поле.");
        if (scheduledTimeUtc == default)
            throw new DomainException("ScheduledTimeUtc обязательное поле.");
        if (format is null)
            throw new DomainException("Регламент матча обязателен.");

        return new Match(
            Guid.NewGuid(),
            stageId,
            tournamentId,
            divisionId,
            groupId,
            homeTeamId,
            awayTeamId,
            scheduledTimeUtc,
            format);
    }

    /// <summary>
    /// Перенос матча и замена команд. Только пока матч не начат и по нему нет событий:
    /// протокол ссылается на команды, а судья мог уже начать вести матч офлайн.
    /// </summary>
    public void Update(
        DateTime scheduledTimeUtc,
        Guid homeTeamId,
        Guid awayTeamId,
        Guid? groupId,
        bool hasEvents)
    {
        EnsureNotStarted(hasEvents, "Изменить");

        if (scheduledTimeUtc == default)
            throw new DomainException("Время начала матча обязательно.");
        if (homeTeamId == Guid.Empty || awayTeamId == Guid.Empty)
            throw new DomainException("Обе команды обязательны.");
        if (homeTeamId == awayTeamId)
            throw new DomainException("Команда не может играть сама с собой.");

        ScheduledTimeUtc = scheduledTimeUtc;
        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
        // Группа следует за командами: при замене команд матч может перейти в другую группу.
        GroupId = groupId;
    }

    /// <summary>
    /// Удалить можно только ошибочно созданный матч: не начатый и без событий.
    /// Сыгранный матч — часть таблицы и статистики.
    /// </summary>
    public void EnsureCanDelete(bool hasEvents) => EnsureNotStarted(hasEvents, "Удалить");

    private void EnsureNotStarted(bool hasEvents, string action)
    {
        if (Status is not (MatchStatus.Scheduled or MatchStatus.Cancelled))
            throw new ConflictException($"{action} можно только не начатый матч.");

        if (hasEvents)
            throw new ConflictException($"{action} матч нельзя: по нему уже есть события в протоколе.");
    }

    /// <summary>Применяет событие жизненного цикла; недопустимый переход даёт DomainException.</summary>
    public void ApplyLifecycle(MatchEvent matchEvent)
    {
        var occurredAtUtc = matchEvent.EffectiveTimeUtc;

        switch (matchEvent.Type)
        {
            case MatchEventType.MatchStarted:
                Start(occurredAtUtc);
                break;
            case MatchEventType.MatchPaused:
                Stop(occurredAtUtc);
                break;
            case MatchEventType.MatchResumed:
                Resume(occurredAtUtc);
                break;
            case MatchEventType.PeriodStarted:
                StartNextPeriod(occurredAtUtc);
                break;
            case MatchEventType.MatchFinished:
                Finish(occurredAtUtc);
                break;
            default:
                throw new DomainException($"Событие '{matchEvent.Type}' не меняет статус матча.");
        }
    }

    public void Start(DateTime occurredAtUtc)
    {
        if (Status != MatchStatus.Scheduled)
            throw new DomainException(
                "Матч можно начать только из запланированного состояния.");

        Status = MatchStatus.InProgress;
        StartedAtUtc = occurredAtUtc;

        CurrentPeriod = 1;
        PeriodStartedAtUtc = occurredAtUtc;
    }

    public void Stop(DateTime occurredAtUtc)
    {
        if (Status != MatchStatus.InProgress)
            throw new DomainException(
                "Матч можно остановить только из состояния в процессе.");

        // Остановка не завершает период: судья может остановить игру внутри периода.
        Status = MatchStatus.Pause;
        PausedAtUtc = occurredAtUtc;
    }

    public void Resume(DateTime occurredAtUtc)
    {
        if (Status != MatchStatus.Pause)
            throw new DomainException(
                "Матч можно возобновить только из состояния перерыва.");

        Status = MatchStatus.InProgress;

        if (PausedAtUtc is { } pausedAtUtc)
            PeriodPausedMilliseconds += Math.Max(0, (long)(occurredAtUtc - pausedAtUtc).TotalMilliseconds);

        PausedAtUtc = null;
    }

    /// <summary>
    /// Переход к следующему периоду. Матч при этом не останавливается: если игра шла,
    /// она продолжается уже в новом периоде. Паузы считаются внутри периода, поэтому счётчик
    /// обнуляется; если матч стоял на паузе, часы нового периода стоят на нуле до продолжения.
    /// </summary>
    public void StartNextPeriod(DateTime occurredAtUtc)
    {
        if (Status != MatchStatus.InProgress && Status != MatchStatus.Pause)
            throw new DomainException(
                "Перейти к следующему периоду можно только в идущем матче.");

        if (CurrentPeriod >= PeriodsCount)
            throw new DomainException(
                $"В матче {PeriodsCount} период(ов), период {CurrentPeriod} — последний.");

        CurrentPeriod++;
        PeriodStartedAtUtc = occurredAtUtc;
        PeriodPausedMilliseconds = 0;
        PausedAtUtc = Status == MatchStatus.Pause ? occurredAtUtc : null;
    }

    /// <summary>
    /// Завершение матча. Судья завершает матч сам, поэтому неполнота периодов не блокирует
    /// завершение — правила матча намеренно минимальны (см. AGENTS.md, «Особенности домена»).
    /// </summary>
    public void Finish(DateTime occurredAtUtc)
    {
        if (Status != MatchStatus.InProgress)
            throw new DomainException(
                "Матч можно закончить только из состояния в процессе.");

        Status = MatchStatus.Finished;
        FinishedAtUtc = occurredAtUtc;
    }

    /// <summary>
    /// Проверяет, что протокол ещё можно менять. По документации после финального свистка
    /// события правит только организатор, но модуля Identity пока нет — поэтому после
    /// завершения протокол закрыт для всех.
    /// </summary>
    public void EnsureEventsCanBeChanged()
    {
        if (Status is MatchStatus.Finished or MatchStatus.Cancelled)
            throw new DomainException(
                "Матч завершён: протокол можно менять только до финального свистка.");
    }

    /// <summary>
    /// Пересчитывает проекцию счёта из протокола: источник истины — лог событий,
    /// а <see cref="HomeScore"/>/<see cref="AwayScore"/> хранят готовый результат, чтобы не
    /// суммировать события на каждый запрос (см. документацию, раздел 9).
    /// Отменённые события (на которые ссылается активный <c>EventVoided</c>) не учитываются,
    /// а автогол засчитывается сопернику команды, в чьи ворота он забит.
    /// </summary>
    public void RebuildScoreProjection(IReadOnlyCollection<MatchEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var voidedEventIds = new HashSet<Guid>();
        foreach (var matchEvent in events)
        {
            EnsureEventBelongsToMatch(matchEvent);

            if (matchEvent.Type == MatchEventType.EventVoided && matchEvent.TargetEventId is { } targetId)
                voidedEventIds.Add(targetId);
        }

        var homeScore = 0;
        var awayScore = 0;
        var eventsForReview = 0;

        foreach (var matchEvent in events)
        {
            if (voidedEventIds.Contains(matchEvent.Id))
                continue;

            if (matchEvent.ReviewNote is not null)
                eventsForReview++;

            switch (matchEvent.Type)
            {
                case MatchEventType.Goal when matchEvent.TeamId == HomeTeamId:
                    homeScore++;
                    break;
                case MatchEventType.Goal when matchEvent.TeamId == AwayTeamId:
                    awayScore++;
                    break;
                case MatchEventType.OwnGoal when matchEvent.TeamId == HomeTeamId:
                    awayScore++;
                    break;
                case MatchEventType.OwnGoal when matchEvent.TeamId == AwayTeamId:
                    homeScore++;
                    break;
            }
        }

        HomeScore = homeScore;
        AwayScore = awayScore;
        EventsForReview = eventsForReview;
    }

    /// <summary>Может ли новое событие войти в протокол: матч открыт, команда играет в этом матче.</summary>
    public void EnsureCanAccept(MatchEvent matchEvent)
    {
        if (!IsLateStatistic(matchEvent))
            EnsureEventsCanBeChanged();

        EnsureEventBelongsToMatch(matchEvent);
    }

    /// <summary>
    /// Статистик и судья отправляют события независимо: удар за минуту до свистка может дойти
    /// до сервера уже после финального свистка судьи. Такой удар — часть матча, его принимаем;
    /// всё, что отмечено после свистка, — нет.
    /// </summary>
    private bool IsLateStatistic(MatchEvent matchEvent) =>
        matchEvent.IsStatistic
        && Status == MatchStatus.Finished
        && FinishedAtUtc is { } finishedAtUtc
        && matchEvent.EffectiveTimeUtc <= finishedAtUtc;

    private void EnsureEventBelongsToMatch(MatchEvent matchEvent)
    {
        if (matchEvent.MatchId != Id)
            throw new DomainException("Событие относится к другому матчу.");

        if (matchEvent.TeamId is { } teamId && teamId != HomeTeamId && teamId != AwayTeamId)
            throw new DomainException("Событие относится к команде, которая не играет в этом матче.");
    }
}
