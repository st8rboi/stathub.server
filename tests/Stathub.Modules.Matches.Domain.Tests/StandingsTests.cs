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
        // При полном равенстве порядок стабилизирован по Id команды.
        Assert.Equal(Alpha, first[0].TeamId);
    }
}
