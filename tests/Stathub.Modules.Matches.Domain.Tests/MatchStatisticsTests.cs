using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Modules.Matches.Domain.Services;
using Stathub.Modules.Matches.Domain.ValueObjects;
using Stathub.Shared.Exceptions;
using Xunit;

namespace Stathub.Modules.Matches.Domain.Tests;

/// <summary>
/// Командная статистика матча (удары, фолы, угловые) — проекция протокола: отмена исключает событие,
/// удар в створ входит и в общее число ударов, на счёт статистика не влияет.
/// </summary>
public sealed class MatchStatisticsTests
{
    private static readonly Guid HomeTeamId = Guid.NewGuid();
    private static readonly Guid AwayTeamId = Guid.NewGuid();

    [Fact]
    public void Compute_CountsEachTeamSeparately_AndShotOnTargetIsAlsoAShot()
    {
        var match = CreateMatch();

        var statistics = MatchStatistics.Compute(HomeTeamId, AwayTeamId, [
            Stat(match, MatchEventType.Shot, HomeTeamId, 1, xg: 0.15m),
            Stat(match, MatchEventType.ShotOnTarget, HomeTeamId, 2, xg: 0.45m),
            Stat(match, MatchEventType.Foul, HomeTeamId, 3),
            Stat(match, MatchEventType.Corner, AwayTeamId, 4),
            Stat(match, MatchEventType.Corner, AwayTeamId, 5),
            Stat(match, MatchEventType.Foul, AwayTeamId, 6)
        ]);

        Assert.Equal(new TeamMatchStatistics(Shots: 2, ShotsOnTarget: 1, Fouls: 1, Corners: 0, ExpectedGoals: 0.60m), statistics.Home);
        Assert.Equal(new TeamMatchStatistics(Shots: 0, ShotsOnTarget: 0, Fouls: 1, Corners: 2, ExpectedGoals: 0m), statistics.Away);
        Assert.False(statistics.IsEmpty);
    }

    [Fact]
    public void Compute_IgnoresVoidedEvents()
    {
        var match = CreateMatch();
        var shot = Stat(match, MatchEventType.ShotOnTarget, HomeTeamId, 1);

        var statistics = MatchStatistics.Compute(HomeTeamId, AwayTeamId, [shot, Void(match, shot, 2)]);

        Assert.Equal(TeamMatchStatistics.Empty, statistics.Home);
        Assert.True(statistics.IsEmpty);
    }

    [Fact]
    public void StatisticEvents_DoNotChangeScore()
    {
        var match = CreateMatch();

        match.RebuildScoreProjection([
            Stat(match, MatchEventType.ShotOnTarget, HomeTeamId, 1),
            Stat(match, MatchEventType.Corner, AwayTeamId, 2)
        ]);

        Assert.Equal(0, match.HomeScore);
        Assert.Equal(0, match.AwayScore);
    }

    [Fact]
    public void VoidedShot_DoesNotAddExpectedGoals()
    {
        var match = CreateMatch();
        var shot = Stat(match, MatchEventType.Shot, AwayTeamId, 1, xg: 0.65m);

        var statistics = MatchStatistics.Compute(HomeTeamId, AwayTeamId, [
            shot,
            Void(match, shot, 2),
            Stat(match, MatchEventType.ShotOnTarget, AwayTeamId, 3, xg: 0.05m)
        ]);

        Assert.Equal(1, statistics.Away.Shots);
        Assert.Equal(0.05m, statistics.Away.ExpectedGoals);
    }

    [Theory]
    [InlineData(MatchEventType.Shot, null)]
    [InlineData(MatchEventType.ShotOnTarget, 0.0)]
    [InlineData(MatchEventType.Shot, 1.5)]
    [InlineData(MatchEventType.Foul, 0.25)]
    public void ExpectedGoals_RequiredForShots_AndOnlyForShots(MatchEventType type, double? xg)
    {
        var match = CreateMatch();

        Assert.Throws<DomainException>(() => Stat(match, type, HomeTeamId, 1, xg: (decimal?)xg, defaultXg: false));
    }

    [Fact]
    public void StatisticEvent_RequiresTeam()
    {
        Assert.Throws<DomainException>(() => MatchEvent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MatchEventType.Foul,
            teamId: null,
            playerId: null,
            relatedPlayerId: null,
            targetEventId: null,
            reason: null,
            DateTime.UtcNow,
            sequenceNumber: 1));
    }

    [Fact]
    public void FinishedMatch_AcceptsStatisticRecordedBeforeFinalWhistle_ButNotAfter()
    {
        var match = CreateMatch();
        var kickOff = DateTime.UtcNow.AddMinutes(-90);
        var finalWhistle = kickOff.AddMinutes(80);

        match.Start(kickOff);
        match.Finish(finalWhistle);

        // Удар за минуту до свистка дошёл до сервера позже свистка судьи — это часть матча.
        match.EnsureCanAccept(Stat(match, MatchEventType.Shot, HomeTeamId, 10, finalWhistle.AddMinutes(-1)));

        Assert.Throws<DomainException>(() =>
            match.EnsureCanAccept(Stat(match, MatchEventType.Shot, HomeTeamId, 11, finalWhistle.AddMinutes(1))));

        // Для голов исключения нет: протокол после свистка закрыт.
        Assert.Throws<DomainException>(() =>
            match.EnsureCanAccept(Stat(match, MatchEventType.Goal, HomeTeamId, 12, finalWhistle.AddMinutes(-1))));
    }

    private static Match CreateMatch() => Match.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        HomeTeamId,
        AwayTeamId,
        DateTime.UtcNow,
        MatchFormat.Create(periodsCount: 2, periodDurationMinutes: 45, extraTimeEnabled: false, penaltyShootoutEnabled: false));

    /// <param name="defaultXg">Удару без явного xG подставляется 0.25 — чтобы тесты подсчёта не зависели от xG.</param>
    private static MatchEvent Stat(
        Match match,
        MatchEventType type,
        Guid teamId,
        long sequenceNumber,
        DateTime? occurredAtUtc = null,
        decimal? xg = null,
        bool defaultXg = true) => MatchEvent.Create(
        Guid.NewGuid(),
        match.Id,
        type,
        teamId,
        playerId: null,
        relatedPlayerId: null,
        targetEventId: null,
        reason: null,
        occurredAtUtc ?? DateTime.UtcNow,
        sequenceNumber,
        expectedGoals: xg ?? (defaultXg && MatchEvent.IsShotType(type) ? 0.25m : null));

    private static MatchEvent Void(Match match, MatchEvent target, long sequenceNumber) => MatchEvent.Create(
        Guid.NewGuid(),
        match.Id,
        MatchEventType.EventVoided,
        teamId: null,
        playerId: null,
        relatedPlayerId: null,
        targetEventId: target.Id,
        reason: "случайное нажатие",
        DateTime.UtcNow,
        sequenceNumber);
}
