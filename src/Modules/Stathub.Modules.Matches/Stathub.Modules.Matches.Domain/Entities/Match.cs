using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Modules.Matches.Domain.ValueObjects;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Matches.Domain.Entities;

public sealed class Match : AggregateRoot
{
    public Guid StageId { get; private set; } // Стадия (этап) турнира
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

    private Match()
    {
    }

    private Match(
        Guid id,
        Guid stageId,
        Guid homeTeamId,
        Guid awayTeamId,
        DateTime scheduledTimeUtc,
        MatchFormat format)
    {
        Id = id;
        StageId = stageId;
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
        Guid homeTeamId,
        Guid awayTeamId,
        DateTime scheduledTimeUtc,
        MatchFormat format)
    {
        if (stageId == Guid.Empty)
            throw new DomainException("StageId обязательное поле.");
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
            homeTeamId,
            awayTeamId,
            scheduledTimeUtc,
            format);
    }

    public void Start()
    {
        if (Status != MatchStatus.Scheduled)
            throw new DomainException(
                "Матч можно начать только из запланированного состояния.");

        Status = MatchStatus.InProgress;
        StartedAtUtc = DateTime.UtcNow;

        CurrentPeriod = 1;
        PeriodStartedAtUtc = StartedAtUtc;
    }

    public void Stop()
    {
        if (Status != MatchStatus.InProgress)
            throw new DomainException(
                "Матч можно остановить только из состояния в процессе.");

        // Остановка не завершает период: судья может остановить игру внутри периода.
        Status = MatchStatus.Pause;
    }

    public void Resume()
    {
        if (Status != MatchStatus.Pause)
            throw new DomainException(
                "Матч можно возобновить только из состояния перерыва.");

        Status = MatchStatus.InProgress;
    }

    /// <summary>
    /// Переход к следующему периоду. Матч при этом не останавливается: если игра шла,
    /// она продолжается уже в новом периоде.
    /// </summary>
    public void StartNextPeriod()
    {
        if (Status != MatchStatus.InProgress && Status != MatchStatus.Pause)
            throw new DomainException(
                "Перейти к следующему периоду можно только в идущем матче.");

        if (CurrentPeriod >= PeriodsCount)
            throw new DomainException(
                $"В матче {PeriodsCount} период(ов), период {CurrentPeriod} — последний.");

        CurrentPeriod++;
        PeriodStartedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Завершение матча. Судья завершает матч сам, поэтому неполнота периодов не блокирует
    /// завершение — правила матча намеренно минимальны (см. AGENTS.md, «Особенности домена»).
    /// </summary>
    public void Finish()
    {
        if (Status != MatchStatus.InProgress)
            throw new DomainException(
                "Матч можно закончить только из состояния в процессе.");

        Status = MatchStatus.Finished;
    }
}
