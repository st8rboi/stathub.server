using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Modules.Matches.Domain.Services;
using Xunit;

namespace Stathub.Modules.Matches.Domain.Tests;

/// <summary>Игровое время событий протокола: паузы не считаются, новый период обнуляет часы.</summary>
public sealed class MatchClockTests
{
    private static readonly DateTime Kickoff = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);

    private static (MatchEventType, DateTime) At(MatchEventType type, int minutes, int seconds = 0) =>
        (type, Kickoff.AddMinutes(minutes).AddSeconds(seconds));

    [Fact]
    public void Compute_CountsFromMatchStart()
    {
        var marks = MatchClock.Compute([
            At(MatchEventType.MatchStarted, 0),
            At(MatchEventType.Goal, 4, 30)
        ]);

        Assert.Null(marks[0]);
        Assert.Equal(new MatchClockMark(1, 270), marks[1]);
    }

    [Fact]
    public void Compute_ExcludesPausesInsidePeriod()
    {
        var marks = MatchClock.Compute([
            At(MatchEventType.MatchStarted, 0),
            At(MatchEventType.MatchPaused, 5),
            At(MatchEventType.MatchResumed, 8),
            At(MatchEventType.YellowCard, 10)
        ]);

        Assert.Equal(new MatchClockMark(1, 7 * 60), marks[3]);
    }

    [Fact]
    public void Compute_EventDuringPause_StaysAtStopMinute()
    {
        var marks = MatchClock.Compute([
            At(MatchEventType.MatchStarted, 0),
            At(MatchEventType.MatchPaused, 5),
            At(MatchEventType.RedCard, 7)
        ]);

        Assert.Equal(new MatchClockMark(1, 5 * 60), marks[2]);
    }

    [Fact]
    public void Compute_NextPeriodResetsClock()
    {
        var marks = MatchClock.Compute([
            At(MatchEventType.MatchStarted, 0),
            At(MatchEventType.MatchPaused, 25),
            At(MatchEventType.PeriodStarted, 30),
            At(MatchEventType.MatchResumed, 31),
            At(MatchEventType.Goal, 33)
        ]);

        // Перерыв между таймами: часы второго тайма стоят до продолжения игры.
        Assert.Equal(new MatchClockMark(2, 2 * 60), marks[4]);
    }

    [Fact]
    public void Compute_EventBeforeStart_HasNoMark()
    {
        var marks = MatchClock.Compute([At(MatchEventType.Goal, 1)]);

        Assert.Null(marks[0]);
    }
}
