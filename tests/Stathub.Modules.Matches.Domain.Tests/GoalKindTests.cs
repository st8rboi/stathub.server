using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Modules.Matches.Domain.ValueObjects;
using Stathub.Modules.Statistics.Domain;
using Stathub.Modules.Statistics.Domain.Enums;
using Stathub.Modules.Statistics.Domain.Models;
using Stathub.Shared.Exceptions;
using Xunit;

namespace Stathub.Modules.Matches.Domain.Tests;

/// <summary>
/// Виды голов: пенальти, бонусный мяч (за два) и нереализованный пенальти —
/// счёт матча, правила протокола и личная статистика игроков.
/// </summary>
public sealed class GoalKindTests
{
    private static readonly Guid HomeTeamId = Guid.NewGuid();
    private static readonly Guid AwayTeamId = Guid.NewGuid();
    private static readonly Guid Scorer = Guid.NewGuid();
    private static readonly Guid Assistant = Guid.NewGuid();

    [Fact]
    public void Create_GoalWithoutKind_IsRegular()
    {
        var goal = Event(MatchEventType.Goal, HomeTeamId);

        Assert.Equal(GoalKind.Regular, goal.GoalKind);
        Assert.Equal(1, goal.ScoreValue);
    }

    [Fact]
    public void RebuildScoreProjection_BonusGoalCountsTwice()
    {
        var match = CreateMatch(bonusGoalEnabled: true);

        match.RebuildScoreProjection(new[]
        {
            Event(MatchEventType.Goal, HomeTeamId, matchId: match.Id, goalKind: GoalKind.Bonus, sequenceNumber: 1),
            Event(MatchEventType.Goal, HomeTeamId, matchId: match.Id, goalKind: GoalKind.Penalty, sequenceNumber: 2),
            Event(MatchEventType.Goal, AwayTeamId, matchId: match.Id, sequenceNumber: 3)
        });

        Assert.Equal(3, match.HomeScore);
        Assert.Equal(1, match.AwayScore);
    }

    [Fact]
    public void RebuildScoreProjection_VoidedBonusGoalIsNotCounted()
    {
        var match = CreateMatch(bonusGoalEnabled: true);
        var bonus = Event(MatchEventType.Goal, HomeTeamId, matchId: match.Id, goalKind: GoalKind.Bonus, sequenceNumber: 1);
        var voided = MatchEvent.Create(
            Guid.NewGuid(), match.Id, MatchEventType.EventVoided, null, null, null, bonus.Id, null, DateTime.UtcNow, 2);

        match.RebuildScoreProjection(new[] { bonus, voided });

        Assert.Equal(0, match.HomeScore);
    }

    [Fact]
    public void RebuildScoreProjection_PenaltyMissedDoesNotChangeScore()
    {
        var match = CreateMatch();

        match.RebuildScoreProjection(new[]
        {
            Event(MatchEventType.PenaltyMissed, HomeTeamId, matchId: match.Id, playerId: Scorer, sequenceNumber: 1)
        });

        Assert.Equal(0, match.HomeScore);
        Assert.Equal(0, match.AwayScore);
    }

    [Fact]
    public void EnsureCanAccept_RejectsBonusGoalWhenStageDoesNotAllowIt()
    {
        var match = CreateMatch(bonusGoalEnabled: false);
        var bonus = Event(MatchEventType.Goal, HomeTeamId, matchId: match.Id, goalKind: GoalKind.Bonus);

        Assert.Throws<DomainException>(() => match.EnsureCanAccept(bonus));
    }

    [Fact]
    public void EnsureCanAccept_AcceptsBonusGoalWhenStageAllowsIt()
    {
        var match = CreateMatch(bonusGoalEnabled: true);
        var bonus = Event(MatchEventType.Goal, HomeTeamId, matchId: match.Id, goalKind: GoalKind.Bonus);

        match.EnsureCanAccept(bonus);
    }

    [Fact]
    public void Create_RejectsAssistOnPenaltyGoal()
    {
        Assert.Throws<DomainException>(() => Event(
            MatchEventType.Goal, HomeTeamId, playerId: Scorer, relatedPlayerId: Assistant, goalKind: GoalKind.Penalty));
    }

    [Theory]
    [InlineData(MatchEventType.OwnGoal)]
    [InlineData(MatchEventType.YellowCard)]
    [InlineData(MatchEventType.PenaltyMissed)]
    public void Create_RejectsGoalKindOnNonGoal(MatchEventType type)
    {
        Assert.Throws<DomainException>(() => Event(type, HomeTeamId, goalKind: GoalKind.Penalty));
    }

    [Fact]
    public void Create_RejectsUnknownGoalKind()
    {
        Assert.Throws<DomainException>(() => Event(MatchEventType.Goal, HomeTeamId, goalKind: (GoalKind)42));
    }

    [Fact]
    public void Create_PenaltyMissedRequiresTeamAndHasNoSecondPlayer()
    {
        Assert.Throws<DomainException>(() => Event(MatchEventType.PenaltyMissed, teamId: null, playerId: Scorer));
        Assert.Throws<DomainException>(() => Event(
            MatchEventType.PenaltyMissed, HomeTeamId, playerId: Scorer, relatedPlayerId: Assistant));
    }

    [Fact]
    public void PlayerStatistics_BonusGoalCountsTwiceForScorerAndAssistant()
    {
        var matchId = Guid.NewGuid();
        var events = new[]
        {
            new TournamentPlayerEvent(matchId, Scorer, HomeTeamId, Assistant, PlayerEventKind.Goal, Value: 2),
            new TournamentPlayerEvent(matchId, Scorer, HomeTeamId, Assistant, PlayerEventKind.Goal)
        };

        var entries = PlayerStatisticsCalculator.Calculate(events);

        Assert.Equal(3, entries.Single(entry => entry.PlayerId == Scorer).Goals);
        Assert.Equal(3, entries.Single(entry => entry.PlayerId == Assistant).Assists);
    }

    [Fact]
    public void PlayerStatistics_CountsPenaltyGoalsAndMisses()
    {
        var matchId = Guid.NewGuid();
        var events = new[]
        {
            new TournamentPlayerEvent(matchId, Scorer, HomeTeamId, null, PlayerEventKind.Goal, IsPenalty: true),
            new TournamentPlayerEvent(matchId, Scorer, HomeTeamId, null, PlayerEventKind.Goal),
            new TournamentPlayerEvent(matchId, Scorer, HomeTeamId, null, PlayerEventKind.PenaltyMissed)
        };

        var scorer = Assert.Single(PlayerStatisticsCalculator.Calculate(events));

        Assert.Equal(2, scorer.Goals);
        Assert.Equal(1, scorer.PenaltyGoals);
        Assert.Equal(1, scorer.PenaltiesMissed);
    }

    private static Match CreateMatch(bool bonusGoalEnabled = false) => Match.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        HomeTeamId,
        AwayTeamId,
        DateTime.UtcNow,
        MatchFormat.Create(
            periodsCount: 2,
            periodDurationMinutes: 45,
            extraTimeEnabled: false,
            penaltyShootoutEnabled: false,
            bonusGoalEnabled: bonusGoalEnabled));

    private static MatchEvent Event(
        MatchEventType type,
        Guid? teamId,
        Guid? matchId = null,
        Guid? playerId = null,
        Guid? relatedPlayerId = null,
        GoalKind? goalKind = null,
        long sequenceNumber = 1) => MatchEvent.Create(
        Guid.NewGuid(),
        matchId ?? Guid.NewGuid(),
        type,
        teamId,
        playerId,
        relatedPlayerId,
        targetEventId: null,
        reason: null,
        DateTime.UtcNow,
        sequenceNumber,
        goalKind: goalKind);
}
