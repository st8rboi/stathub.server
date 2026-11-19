using Stathub.Modules.Standings.Domain;
using Stathub.Modules.Standings.Domain.Enums;
using Stathub.Modules.Standings.Domain.Models;
using Xunit;

namespace Stathub.Modules.Matches.Domain.Tests;

/// <summary>
/// Расчёт турнирной таблицы: очки из правил этапа, порядок по тай-брейкам,
/// личные встречи и детерминированный порядок при полном равенстве (§8, §9.2).
/// </summary>
public sealed class StandingsTests
{
    private static readonly Guid Alpha = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Beta = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Gamma = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly StandingsRules Classic = new(
        WinPoints: 3,
        DrawPoints: 1,
        LossPoints: 0,
        Tiebreakers: [StandingsTiebreaker.GoalDifference, StandingsTiebreaker.GoalsScored]);

    [Fact]
    public void Calculate_CountsPointsFromRules()
    {
        var matches = new[]
        {
            new StandingsMatch(Alpha, Beta, 2, 1),
            new StandingsMatch(Alpha, Gamma, 0, 0)
        };

        var table = StandingsCalculator.Calculate(matches, Classic);

        var alpha = table.Single(row => row.TeamId == Alpha);
        Assert.Equal(2, alpha.Played);
        Assert.Equal(1, alpha.Wins);
        Assert.Equal(1, alpha.Draws);
        Assert.Equal(4, alpha.Points);
        Assert.Equal(1, alpha.GoalDifference);
    }

    [Fact]
    public void CalculateTables_ShowsParticipantsWithoutMatchesWithZeros()
    {
        var rules = Classic with { TeamIds = [Alpha, Beta, Gamma] };
        var matches = new[] { new StandingsMatch(Beta, Gamma, 1, 0) };

        var table = Assert.Single(StandingsCalculator.CalculateTables(matches, rules));

        Assert.Equal(3, table.Entries.Count);
        Assert.Equal(Beta, table.Entries[0].TeamId);
        var alpha = table.Entries.Single(row => row.TeamId == Alpha);
        Assert.Equal(0, alpha.Played);
        Assert.Equal(0, alpha.Points);
    }

    [Fact]
    public void Calculate_OrdersByGoalDifferenceWhenPointsEqual()
    {
        var matches = new[]
        {
            new StandingsMatch(Alpha, Gamma, 3, 0),
            new StandingsMatch(Beta, Gamma, 1, 0)
        };

        var table = StandingsCalculator.Calculate(matches, Classic);

        Assert.Equal(Alpha, table[0].TeamId);
        Assert.Equal(Beta, table[1].TeamId);
    }

    [Fact]
    public void Calculate_UsesGoalsScoredWhenDifferenceEqual()
    {
        var rules = new StandingsRules(3, 1, 0, [StandingsTiebreaker.GoalsScored]);

        var matches = new[]
        {
            new StandingsMatch(Alpha, Gamma, 3, 2),
            new StandingsMatch(Beta, Gamma, 2, 1)
        };

        var table = StandingsCalculator.Calculate(matches, rules);

        Assert.Equal(Alpha, table[0].TeamId);
        Assert.Equal(3, table[0].GoalsFor);
    }

    [Fact]
    public void Calculate_UsesHeadToHeadWhenConfigured()
    {
        var rules = new StandingsRules(3, 1, 0, [StandingsTiebreaker.HeadToHead]);

        var matches = new[]
        {
            // Обе обыграли Gamma, но в личной встрече сильнее Beta.
            new StandingsMatch(Alpha, Gamma, 2, 0),
            new StandingsMatch(Beta, Gamma, 2, 0),
            new StandingsMatch(Alpha, Beta, 1, 3)
        };

        var table = StandingsCalculator.Calculate(matches, rules);

        Assert.Equal(Beta, table[0].TeamId);
        Assert.Equal(Alpha, table[1].TeamId);
    }

    [Fact]
    public void Calculate_ReturnsEmptyTableWithoutMatches()
    {
        var table = StandingsCalculator.Calculate([], Classic);

        Assert.Empty(table);
    }

    [Fact]
    public void Calculate_IsDeterministicForFullyEqualTeams()
    {
        var matches = new[]
        {
            new StandingsMatch(Alpha, Gamma, 1, 1),
            new StandingsMatch(Beta, Gamma, 1, 1)
        };

        var first = StandingsCalculator.Calculate(matches, Classic);
        var second = StandingsCalculator.Calculate(matches, Classic);

        Assert.Equal(first.Select(row => row.TeamId), second.Select(row => row.TeamId));
        // Gamma сыграла два матча и набрала больше очков; Alpha и Beta равны по всем критериям,
        // и порядок между ними стабилизирован по Id команды.
        Assert.Equal([Gamma, Alpha, Beta], first.Select(row => row.TeamId));
    }

    [Fact]
    public void Calculate_CountsLiveMatchWithCurrentScore()
    {
        var matches = new[]
        {
            new StandingsMatch(Alpha, Beta, 1, 0),
            new StandingsMatch(Gamma, Alpha, 2, 0, IsLive: true)
        };

        var table = StandingsCalculator.Calculate(matches, Classic);

        // Gamma выигрывает идущий матч — в live-таблице она уже впереди Alpha.
        Assert.Equal([Gamma, Alpha, Beta], table.Select(row => row.TeamId));
        var alpha = table.Single(row => row.TeamId == Alpha);
        Assert.Equal(2, alpha.Played);
        Assert.Equal(1, alpha.Losses);
    }

    [Fact]
    public void Calculate_ReordersAfterGoalInLiveMatch()
    {
        var finished = new StandingsMatch(Alpha, Gamma, 1, 0);

        var before = StandingsCalculator.Calculate(
            [finished, new StandingsMatch(Beta, Gamma, 0, 0, IsLive: true)], Classic);
        var afterGoal = StandingsCalculator.Calculate(
            [finished, new StandingsMatch(Beta, Gamma, 0, 1, IsLive: true)], Classic);

        Assert.Equal([Alpha, Beta, Gamma], before.Select(row => row.TeamId));
        Assert.Equal([Alpha, Gamma, Beta], afterGoal.Select(row => row.TeamId));
    }

    [Theory]
    [InlineData(2, 1, LiveOutcome.Win, LiveOutcome.Loss)]
    [InlineData(1, 1, LiveOutcome.Draw, LiveOutcome.Draw)]
    [InlineData(0, 3, LiveOutcome.Loss, LiveOutcome.Win)]
    public void OutcomeFor_IsSeenFromTeamSide(int home, int away, LiveOutcome homeOutcome, LiveOutcome awayOutcome)
    {
        var match = new StandingsMatch(Alpha, Beta, home, away, IsLive: true);

        Assert.Equal(homeOutcome, match.OutcomeFor(Alpha));
        Assert.Equal(awayOutcome, match.OutcomeFor(Beta));
        Assert.Equal((away, home), match.ScoreFor(Beta));
    }
}
