using Stathub.Modules.Statistics.Domain;
using Stathub.Modules.Statistics.Domain.Enums;
using Stathub.Modules.Statistics.Domain.Models;
using Xunit;

namespace Stathub.Modules.Matches.Domain.Tests;

/// <summary>
/// Статистика игроков на странице команды: только события этой команды и только в матчах этапа.
/// </summary>
public sealed class TeamStatisticsTests
{
    private static readonly Guid Alpha = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Beta = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid Ivanov = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Petrov = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    private static readonly Guid StageMatch1 = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid StageMatch2 = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid OtherStageMatch = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003");

    [Fact]
    public void CalculateForTeam_CountsOnlyEventsOfTeamInGivenMatches()
    {
        var events = new[]
        {
            new TournamentPlayerEvent(StageMatch1, Ivanov, Alpha, Petrov, PlayerEventKind.Goal),
            new TournamentPlayerEvent(StageMatch2, Ivanov, Alpha, null, PlayerEventKind.YellowCard),
            // Матч другого дивизиона того же турнира — не в счёт.
            new TournamentPlayerEvent(OtherStageMatch, Ivanov, Alpha, null, PlayerEventKind.Goal)
        };

        var entries = PlayerStatisticsCalculator.CalculateForTeam(events, Alpha, new HashSet<Guid> { StageMatch1, StageMatch2 });

        var ivanov = entries.Single(entry => entry.PlayerId == Ivanov);
        Assert.Equal(1, ivanov.Goals);
        Assert.Equal(1, ivanov.YellowCards);
        Assert.Equal(2, ivanov.Matches);

        var petrov = entries.Single(entry => entry.PlayerId == Petrov);
        Assert.Equal(1, petrov.Assists);
    }

    [Fact]
    public void CalculateForTeam_TransferredPlayerKeepsOnlyThisTeamEvents()
    {
        var events = new[]
        {
            // До перехода Иванов забил за Бету, после — за Альфу.
            new TournamentPlayerEvent(StageMatch1, Ivanov, Beta, null, PlayerEventKind.Goal),
            new TournamentPlayerEvent(StageMatch2, Ivanov, Alpha, null, PlayerEventKind.OwnGoal)
        };

        var entries = PlayerStatisticsCalculator.CalculateForTeam(events, Alpha, new HashSet<Guid> { StageMatch1, StageMatch2 });

        var ivanov = Assert.Single(entries);
        Assert.Equal(0, ivanov.Goals);
        Assert.Equal(1, ivanov.OwnGoals);
        Assert.Equal(1, ivanov.Matches);
    }
}
