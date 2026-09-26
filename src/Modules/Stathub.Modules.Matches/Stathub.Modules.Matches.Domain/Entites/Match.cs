using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Matches.Domain.Entities;

public sealed class Match : AggregateRoot
{

    public Guid StageId { get; private set; } // Стадия (этап) турнира
    public Guid HomeTeamId { get; private set; } // Первая команда
    public Guid AwayTeamId { get; private set; } // Вторая команда

    public DateTime CreatedAtUtc { get; private set; } // Создан в
    public DateTime ScheduledTimeUtc { get; private set; } // Запланирова на
    public DateTime? StartedAtUtc { get; private set; } // Реально начался в

    public MatchStatus Status { get; private set; } // Завершен, Запланирован, В процессе, Отменен

    public int HomeScore { get; private set; } // Счет первой команды
    public int AwayScore { get; private set; } // Счет второй команды

    private Match()
    {
    }

    private Match(
        Guid id,
        Guid stageId,
        Guid homeTeamId,
        Guid awayTeamId,
        DateTime scheduledTimeUtc)
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
    }

    public static Match Create(
        Guid stageId,
        Guid homeTeamId,
        Guid awayTeamId,
        DateTime scheduledTimeUtc)
    {
        if (stageId == Guid.Empty) 
            throw new DomainException("DtageId обязательное поле."); 
        if (homeTeamId == Guid.Empty) 
            throw new DomainException("HomeTeamId обязательное поле."); 
        if (awayTeamId == Guid.Empty) 
            throw new DomainException("AwayTeamId обязательное поле."); 
        if (scheduledTimeUtc == default) 
            throw new DomainException("ScheduledTimeUtc обязательное поле.");

        return new Match(
            Guid.NewGuid(),
            stageId,
            homeTeamId,
            awayTeamId,
            scheduledTimeUtc);
    }

    // public void Start, Stop, Resume, Finish
}