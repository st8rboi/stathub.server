using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stathub.WebApi.IntegrationTests;

/// <summary>
/// Статистик: видит только назначенные матчи, отмечает удары, фолы и угловые,
/// но не управляет матчем; зритель видит статистику отдельно от протокола.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class StatisticianTests(StathubApiFactory factory)
{
    [Fact]
    public async Task Statistician_RecordsStatistics_ButCannotControlMatchOrScore()
    {
        var scenario = await CreateScenarioAsync();
        var now = DateTime.UtcNow;

        var result = await SendAsync(scenario.Statistician, scenario.MatchId, [
            Event("ShotOnTarget", 1, now, scenario.HomeTeamId, 0.45m),
            Event("Corner", 2, now, scenario.AwayTeamId),
            Event("MatchStarted", 3, now),
            Event("Goal", 4, now, scenario.HomeTeamId)
        ]);

        var state = result.GetProperty("state");
        var home = state.GetProperty("statistics").GetProperty("home");

        Assert.Equal(2, result.GetProperty("acceptedEventIds").GetArrayLength());
        Assert.Equal(2, result.GetProperty("rejectedEvents").GetArrayLength());
        Assert.Equal("Scheduled", state.GetProperty("match").GetProperty("status").GetString());
        Assert.Equal(0, state.GetProperty("match").GetProperty("homeScore").GetInt32());
        Assert.Equal(1, home.GetProperty("shots").GetInt32());
        Assert.Equal(1, home.GetProperty("shotsOnTarget").GetInt32());
        Assert.Equal(0.45m, home.GetProperty("expectedGoals").GetDecimal());
    }

    [Fact]
    public async Task Shot_WithoutExpectedGoals_IsRejected()
    {
        var scenario = await CreateScenarioAsync();

        var result = await SendAsync(scenario.Statistician, scenario.MatchId, [
            Event("Shot", 1, DateTime.UtcNow, scenario.HomeTeamId)
        ]);

        Assert.Single(result.GetProperty("rejectedEvents").EnumerateArray());
    }

    [Fact]
    public async Task Statistician_CanVoidOwnStatistic_ButNotRefereeEvents()
    {
        var scenario = await CreateScenarioAsync();
        var now = DateTime.UtcNow;

        var goal = Event("Goal", 2, now, scenario.HomeTeamId);
        await SendAsync(scenario.Organizer.Client, scenario.MatchId, [Event("MatchStarted", 1, now.AddMinutes(-1)), goal]);

        var foul = Event("Foul", 3, now, scenario.AwayTeamId);
        var result = await SendAsync(scenario.Statistician, scenario.MatchId, [
            foul,
            Void(4, now, foul.id),
            Void(5, now, goal.id)
        ]);

        var state = result.GetProperty("state");

        Assert.Equal(2, result.GetProperty("acceptedEventIds").GetArrayLength());
        Assert.Single(result.GetProperty("rejectedEvents").EnumerateArray());
        Assert.Equal(0, state.GetProperty("statistics").GetProperty("away").GetProperty("fouls").GetInt32());
        Assert.Equal(1, state.GetProperty("match").GetProperty("homeScore").GetInt32());
    }

    [Fact]
    public async Task Statistician_SeesOnlyAssignedMatches()
    {
        var scenario = await CreateScenarioAsync();
        var otherMatchId = await scenario.Organizer.CreateMatchAsync(
            scenario.StageId,
            scenario.HomeTeamId,
            scenario.AwayTeamId);

        var mine = await scenario.Statistician.GetAsync("/referee/matches");
        var foreign = await scenario.Statistician.GetAsync($"/matches/{otherMatchId}/state");

        using var document = JsonDocument.Parse(await mine.Content.ReadAsStringAsync());
        var ids = document.RootElement.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();

        Assert.Equal([scenario.MatchId], ids);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }

    [Fact]
    public async Task PublicMatch_ShowsStatisticsSeparately_FromProtocol()
    {
        var scenario = await CreateScenarioAsync();
        var now = DateTime.UtcNow;

        (await scenario.Organizer.Client.PostAsync($"/leagues/{scenario.Organizer.LeagueId}/publish", null))
            .EnsureSuccessStatusCode();

        await SendAsync(scenario.Statistician, scenario.MatchId, [
            Event("Shot", 1, now, scenario.HomeTeamId, 0.15m),
            Event("Corner", 2, now, scenario.HomeTeamId)
        ]);

        var response = await factory.CreateClient().GetAsync($"/public/matches/{scenario.MatchId}");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Empty(root.GetProperty("events").EnumerateArray());
        Assert.Equal(1, root.GetProperty("statistics").GetProperty("home").GetProperty("corners").GetInt32());
    }

    private static EventBody Event(
        string type,
        long sequenceNumber,
        DateTime clientTimestampUtc,
        Guid? teamId = null,
        decimal? expectedGoals = null) =>
        new(Guid.NewGuid(), type, teamId, null, clientTimestampUtc, sequenceNumber, expectedGoals);

    private static EventBody Void(long sequenceNumber, DateTime clientTimestampUtc, Guid targetEventId) =>
        new(Guid.NewGuid(), "EventVoided", null, targetEventId, clientTimestampUtc, sequenceNumber, null);

    private static async Task<JsonElement> SendAsync(HttpClient client, Guid matchId, object[] events)
    {
        var response = await client.PostAsJsonAsync($"/matches/{matchId}/events", new { events });
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private async Task<Scenario> CreateScenarioAsync()
    {
        var organizer = await factory.CreateOrganizerAsync();
        var tournamentId = await organizer.CreateTournamentAsync();
        var stageId = await organizer.CreateStageAsync(tournamentId);

        var homeTeamId = await organizer.CreateTeamAsync();
        var awayTeamId = await organizer.CreateTeamAsync();

        await organizer.RegisterTeamAsync(stageId, homeTeamId);
        await organizer.RegisterTeamAsync(stageId, awayTeamId);

        var matchId = await organizer.CreateMatchAsync(stageId, homeTeamId, awayTeamId);
        var statistician = await organizer.CreateStaffAsync("Statistician", matchId);

        return new Scenario(organizer, statistician, stageId, homeTeamId, awayTeamId, matchId);
    }

    private sealed record EventBody(
        Guid id,
        string type,
        Guid? teamId,
        Guid? targetEventId,
        DateTime clientTimestampUtc,
        long sequenceNumber,
        decimal? expectedGoals);

    private sealed record Scenario(
        TestOrganizer Organizer,
        HttpClient Statistician,
        Guid StageId,
        Guid HomeTeamId,
        Guid AwayTeamId,
        Guid MatchId);
}
