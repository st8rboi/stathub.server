using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Modules.Matches.Domain.ValueObjects;
using Stathub.Shared.Exceptions;
using Xunit;

namespace Stathub.Modules.Matches.Domain.Tests;

/// <summary>
/// Доменные правила протокола: счёт — проекция лога событий, отмена не удаляет событие,
/// а отменённые события не учитываются в счёте.
/// </summary>
public sealed class MatchEventsTests
{
    private static readonly Guid HomeTeamId = Guid.NewGuid();
    private static readonly Guid AwayTeamId = Guid.NewGuid();

    [Fact]
    public void RebuildScoreProjection_CountsGoalsForBothTeams()
    {
        var match = CreateMatch();

        match.RebuildScoreProjection(new[]
        {
            Goal(match, HomeTeamId, 1),
            Goal(match, HomeTeamId, 2),
            Goal(match, AwayTeamId, 3)
        });

        Assert.Equal(2, match.HomeScore);
        Assert.Equal(1, match.AwayScore);
    }

    [Fact]
    public void RebuildScoreProjection_CreditsOwnGoalToOpponent()
    {
        var match = CreateMatch();

        // Автогол игрока хозяев засчитывается гостям.
        match.RebuildScoreProjection(new[] { OwnGoal(match, HomeTeamId, 1) });

        Assert.Equal(0, match.HomeScore);
        Assert.Equal(1, match.AwayScore);
    }

    [Fact]
    public void RebuildScoreProjection_IgnoresVoidedGoal()
    {
        var match = CreateMatch();
        var goal = Goal(match, HomeTeamId, 1);

        match.RebuildScoreProjection(new[] { goal, VoidGoal(match, goal, 2) });

        Assert.Equal(0, match.HomeScore);
        Assert.Equal(0, match.AwayScore);
    }

    [Fact]
    public void RebuildScoreProjection_IsIdempotent()
    {
        var match = CreateMatch();
        var events = new[] { Goal(match, HomeTeamId, 1), Goal(match, AwayTeamId, 2) };

        match.RebuildScoreProjection(events);
        match.RebuildScoreProjection(events);

        Assert.Equal(1, match.HomeScore);
        Assert.Equal(1, match.AwayScore);
    }

    [Fact]
    public void RebuildScoreProjection_ThrowsForTeamOutsideMatch()
    {
        var match = CreateMatch();
        var foreignEvent = Goal(match, Guid.NewGuid(), 1);

        Assert.Throws<DomainException>(() => match.RebuildScoreProjection(new[] { foreignEvent }));
    }

    [Fact]
    public void RebuildScoreProjection_ThrowsForEventOfAnotherMatch()
    {
        var match = CreateMatch();
        var otherMatch = CreateMatch();
        var foreignEvent = Goal(otherMatch, HomeTeamId, 1);

        Assert.Throws<DomainException>(() => match.RebuildScoreProjection(new[] { foreignEvent }));
    }

    [Fact]
    public void EnsureEventsCanBeChanged_ThrowsAfterFinish()
    {
        var match = CreateMatch();
        match.Start();
        match.Finish();

        Assert.Throws<DomainException>(match.EnsureEventsCanBeChanged);
    }

    [Fact]
    public void EnsureEventsCanBeChanged_AllowsRunningMatch()
    {
        var match = CreateMatch();
        match.Start();

        match.EnsureEventsCanBeChanged();
    }

    [Fact]
    public void Create_WithoutClientGeneratedId_Throws()
    {
        Assert.Throws<DomainException>(() => MatchEvent.Create(
            Guid.Empty,
            Guid.NewGuid(),
            MatchEventType.Goal,
            HomeTeamId,
            playerId: null,
            relatedPlayerId: null,
            targetEventId: null,
            reason: null,
            DateTime.UtcNow,
            sequenceNumber: 1));
    }

    [Fact]
    public void Create_GoalWithoutTeam_Throws()
    {
        Assert.Throws<DomainException>(() => MatchEvent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MatchEventType.Goal,
            teamId: null,
            playerId: null,
            relatedPlayerId: null,
            targetEventId: null,
            reason: null,
            DateTime.UtcNow,
            sequenceNumber: 1));
    }

    [Fact]
    public void Create_VoidWithoutTarget_Throws()
    {
        Assert.Throws<DomainException>(() => MatchEvent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MatchEventType.EventVoided,
            teamId: null,
            playerId: null,
            relatedPlayerId: null,
            targetEventId: null,
            reason: "ошибочное событие",
            DateTime.UtcNow,
            sequenceNumber: 1));
    }

    [Fact]
    public void Create_VoidOfItself_Throws()
    {
        var id = Guid.NewGuid();

        Assert.Throws<DomainException>(() => MatchEvent.Create(
            id,
            Guid.NewGuid(),
            MatchEventType.EventVoided,
            teamId: null,
            playerId: null,
            relatedPlayerId: null,
            targetEventId: id,
            reason: null,
            DateTime.UtcNow,
            sequenceNumber: 1));
    }

    [Fact]
    public void Create_WithNonPositiveSequenceNumber_Throws()
    {
        Assert.Throws<DomainException>(() => MatchEvent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MatchEventType.YellowCard,
            HomeTeamId,
            playerId: null,
            relatedPlayerId: null,
            targetEventId: null,
            reason: null,
            DateTime.UtcNow,
            sequenceNumber: 0));
    }

    private static Match CreateMatch() => Match.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        HomeTeamId,
        AwayTeamId,
        DateTime.UtcNow,
        MatchFormat.Create(periodsCount: 2, periodDurationMinutes: 45, extraTimeEnabled: false, penaltyShootoutEnabled: false));

    private static MatchEvent Goal(Match match, Guid teamId, long sequenceNumber) => MatchEvent.Create(
        Guid.NewGuid(),
        match.Id,
        MatchEventType.Goal,
        teamId,
        playerId: null,
        relatedPlayerId: null,
        targetEventId: null,
        reason: null,
        DateTime.UtcNow,
        sequenceNumber);

    private static MatchEvent OwnGoal(Match match, Guid teamId, long sequenceNumber) => MatchEvent.Create(
        Guid.NewGuid(),
        match.Id,
        MatchEventType.OwnGoal,
        teamId,
        playerId: null,
        relatedPlayerId: null,
        targetEventId: null,
        reason: null,
        DateTime.UtcNow,
        sequenceNumber);

    private static MatchEvent VoidGoal(Match match, MatchEvent target, long sequenceNumber) => MatchEvent.Create(
        Guid.NewGuid(),
        match.Id,
        MatchEventType.EventVoided,
        teamId: null,
        playerId: null,
        relatedPlayerId: null,
        targetEventId: target.Id,
        reason: "ошибка судьи",
        DateTime.UtcNow,
        sequenceNumber);
}
