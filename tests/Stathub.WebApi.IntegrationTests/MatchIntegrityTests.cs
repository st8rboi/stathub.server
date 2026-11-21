using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Stathub.WebApi.IntegrationTests;

/// <summary>
/// Целостность протокола: параллельные батчи не теряют счёт, кривой ввод не роняет батч в 500,
/// игроки вне заявки и дисквалифицированные помечаются, отменённый матч выпадает из витрины.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MatchIntegrityTests(StathubApiFactory factory)
{
    [Fact]
    public async Task ParallelBatches_KeepScoreInSyncWithProtocol()
    {
        var scenario = await CreateScenarioAsync();
        var kickOff = DateTime.UtcNow.AddMinutes(-30);
        await SendAsync(scenario, [Event("MatchStarted", 1, kickOff)]);

        var batches = Enumerable.Range(0, 6)
            .Select(index => SendAsync(scenario, [Event("Goal", 2 + index, kickOff.AddMinutes(index + 1), scenario.HomeTeamId)]));
        await Task.WhenAll(batches);

        var state = await GetStateAsync(scenario);

        Assert.Equal(6, state.GetProperty("match").GetProperty("homeScore").GetInt32());
    }

    [Fact]
    public async Task SameBatchSentTwiceAtOnce_IsAcceptedOnce_WithoutServerError()
    {
        var scenario = await CreateScenarioAsync();
        var kickOff = DateTime.UtcNow.AddMinutes(-30);
        await SendAsync(scenario, [Event("MatchStarted", 1, kickOff)]);

        object[] batch = [Event("Goal", 2, kickOff.AddMinutes(1), scenario.HomeTeamId)];
        var responses = await Task.WhenAll(PostAsync(scenario, batch), PostAsync(scenario, batch));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(1, (await GetStateAsync(scenario)).GetProperty("match").GetProperty("homeScore").GetInt32());
    }

    [Fact]
    public async Task EventIdTakenByAnotherMatch_IsRejectedAlone()
    {
        var scenario = await CreateScenarioAsync();
        var otherMatchId = await scenario.Organizer.CreateMatchAsync(scenario.StageId, scenario.AwayTeamId, scenario.HomeTeamId);
        var now = DateTime.UtcNow;

        var shared = Event("MatchStarted", 1, now.AddMinutes(-5));
        await SendAsync(scenario, [shared]);

        var result = await SendAsync(scenario with { MatchId = otherMatchId }, [shared with { sequenceNumber = 1 }, Event("MatchStarted", 2, now.AddMinutes(-4))]);

        Assert.Equal(shared.id, Assert.Single(result.GetProperty("rejectedEvents").EnumerateArray()).GetProperty("eventId").GetGuid());
        Assert.Equal("InProgress", result.GetProperty("state").GetProperty("match").GetProperty("status").GetString());
    }

    [Fact]
    public async Task NullInsideEventList_IsBadRequest()
    {
        var scenario = await CreateScenarioAsync();

        var response = await scenario.Organizer.Client.PostAsync(
            $"/matches/{scenario.MatchId}/events",
            new StringContent("{\"events\":[null]}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TimeWithoutZone_IsTreatedAsUtc()
    {
        var scenario = await CreateScenarioAsync();
        var kickOff = DateTime.UtcNow.AddMinutes(-10);

        var body = $"{{\"events\":[{{\"id\":\"{Guid.NewGuid()}\",\"type\":\"MatchStarted\"," +
                   $"\"clientTimestampUtc\":\"{kickOff:yyyy-MM-ddTHH:mm:ss}\",\"sequenceNumber\":1}}]}}";
        var events = await scenario.Organizer.Client.PostAsync(
            $"/matches/{scenario.MatchId}/events",
            new StringContent(body, Encoding.UTF8, "application/json"));

        var created = await scenario.Organizer.Client.PostAsync(
            "/matches",
            new StringContent(
                $"{{\"stageId\":\"{scenario.StageId}\",\"homeTeamId\":\"{scenario.HomeTeamId}\"," +
                $"\"awayTeamId\":\"{scenario.AwayTeamId}\",\"startTimeUtc\":\"2026-11-21T12:00:00\"}}",
                Encoding.UTF8,
                "application/json"));

        Assert.Equal(HttpStatusCode.OK, events.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var match = JsonDocument.Parse(
            await scenario.Organizer.Client.GetStringAsync($"/matches/{await created.ReadIdAsync()}"));
        Assert.Equal(
            new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc),
            match.RootElement.GetProperty("scheduledTimeUtc").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task PlayerOutsideRoster_IsAccepted_WithReviewNote()
    {
        var scenario = await CreateScenarioAsync();
        var registered = await scenario.Organizer.CreatePlayerAsync();
        var outsider = await scenario.Organizer.CreatePlayerAsync();
        await scenario.Organizer.RegisterPlayerAsync(scenario.HomeTeamId, registered);
        var now = DateTime.UtcNow;

        var result = await SendAsync(scenario, [
            Event("MatchStarted", 1, now.AddMinutes(-5)),
            Event("Goal", 2, now.AddMinutes(-4), scenario.HomeTeamId, registered),
            Event("Goal", 3, now.AddMinutes(-3), scenario.HomeTeamId, outsider),
            Event("Goal", 4, now.AddMinutes(-2), scenario.AwayTeamId, registered)
        ]);
        var state = result.GetProperty("state");
        var notes = state.GetProperty("events").EnumerateArray()
            .Where(item => item.GetProperty("type").GetString() == "Goal")
            .Select(item => item.GetProperty("reviewNote").ValueKind != JsonValueKind.Null)
            .ToList();

        Assert.Equal(new[] { false, true, true }, notes);
        Assert.Equal(2, state.GetProperty("match").GetProperty("homeScore").GetInt32());
        Assert.Equal(2, state.GetProperty("match").GetProperty("eventsForReview").GetInt32());
    }

    [Fact]
    public async Task Ban_FollowsPlayer_AfterTransfer()
    {
        var scenario = await CreateScenarioAsync();
        var playerId = await scenario.Organizer.CreatePlayerAsync();
        await scenario.Organizer.RegisterPlayerAsync(scenario.HomeTeamId, playerId);

        await (await scenario.Organizer.Client.PostAsJsonAsync(
            $"/discipline/tournaments/{scenario.TournamentId}",
            new { teamId = scenario.HomeTeamId, playerId, reason = "Red card", matchesBanned = 2 })).ReadIdAsync();

        var transfer = await scenario.Organizer.Client.PostAsJsonAsync(
            $"/divisions/{scenario.Organizer.GetDivisionId(scenario.StageId)}/transfers",
            new { playerId, fromTeamId = scenario.HomeTeamId, toTeamId = scenario.AwayTeamId });
        transfer.EnsureSuccessStatusCode();

        var now = DateTime.UtcNow;
        var result = await SendAsync(scenario, [
            Event("MatchStarted", 1, now.AddMinutes(-5)),
            Event("Goal", 2, now.AddMinutes(-4), scenario.AwayTeamId, playerId)
        ]);

        var goal = result.GetProperty("state").GetProperty("events")[1];
        Assert.Contains("дисквалифицированный", goal.GetProperty("reviewNote").GetString());
    }

    [Fact]
    public async Task GoalForOldTeam_OnTransferDay_IsMarked()
    {
        var scenario = await CreateScenarioAsync();
        var playerId = await scenario.Organizer.CreatePlayerAsync();
        await scenario.Organizer.RegisterPlayerAsync(scenario.HomeTeamId, playerId);

        var transfer = await scenario.Organizer.Client.PostAsJsonAsync(
            $"/divisions/{scenario.Organizer.GetDivisionId(scenario.StageId)}/transfers",
            new { playerId, fromTeamId = scenario.HomeTeamId, toTeamId = scenario.AwayTeamId });
        transfer.EnsureSuccessStatusCode();

        var now = DateTime.UtcNow;
        var result = await SendAsync(scenario, [
            Event("MatchStarted", 1, now.AddMinutes(-5)),
            Event("Goal", 2, now.AddMinutes(-4), scenario.HomeTeamId, playerId),
            Event("Goal", 3, now.AddMinutes(-3), scenario.AwayTeamId, playerId)
        ]);
        var goals = result.GetProperty("state").GetProperty("events");

        Assert.Equal(JsonValueKind.String, goals[1].GetProperty("reviewNote").ValueKind);
        Assert.Equal(JsonValueKind.Null, goals[2].GetProperty("reviewNote").ValueKind);
    }

    [Fact]
    public async Task Disqualification_OfPlayerNotInTeam_IsRejected()
    {
        var scenario = await CreateScenarioAsync();
        var playerId = await scenario.Organizer.CreatePlayerAsync();
        await scenario.Organizer.RegisterPlayerAsync(scenario.AwayTeamId, playerId);

        var response = await scenario.Organizer.Client.PostAsJsonAsync(
            $"/discipline/tournaments/{scenario.TournamentId}",
            new { teamId = scenario.HomeTeamId, playerId, reason = "Red card", matchesBanned = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CancelledMatch_LeavesLiveStandingsAndStatistics_AndRejectsLateEvents()
    {
        var scenario = await CreateScenarioAsync();
        var playerId = await scenario.Organizer.CreatePlayerAsync();
        await scenario.Organizer.RegisterPlayerAsync(scenario.HomeTeamId, playerId);
        await (await scenario.Organizer.Client.PostAsJsonAsync($"/leagues/{scenario.Organizer.LeagueId}/publish", new { }))
            .Content.ReadAsStringAsync();

        var now = DateTime.UtcNow;
        await SendAsync(scenario, [
            Event("MatchStarted", 1, now.AddMinutes(-5)),
            Event("Goal", 2, now.AddMinutes(-4), scenario.HomeTeamId, playerId)
        ]);

        var cancel = await scenario.Organizer.Client.PostAsync($"/matches/{scenario.MatchId}/cancel", null);
        var again = await scenario.Organizer.Client.PostAsync($"/matches/{scenario.MatchId}/cancel", null);
        var late = await SendAsync(scenario, [Event("Goal", 3, now.AddMinutes(-3), scenario.HomeTeamId, playerId)]);

        var anonymous = factory.CreateClient();
        var live = await anonymous.GetStringAsync("/public/live");
        using var stage = JsonDocument.Parse(await anonymous.GetStringAsync($"/public/stages/{scenario.StageId}"));
        using var statistics = JsonDocument.Parse(
            await anonymous.GetStringAsync($"/public/tournaments/{scenario.TournamentId}/statistics"));

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Single(late.GetProperty("rejectedEvents").EnumerateArray());
        Assert.Equal("Cancelled", late.GetProperty("state").GetProperty("match").GetProperty("status").GetString());
        Assert.DoesNotContain(scenario.MatchId.ToString(), live);
        Assert.All(
            stage.RootElement.GetProperty("standings").EnumerateArray(),
            row => Assert.Equal(0, row.GetProperty("played").GetInt32()));
        Assert.Empty(statistics.RootElement.GetProperty("rows").EnumerateArray());
    }

    private static EventBody Event(string type, long sequenceNumber, DateTime clientTimestampUtc, Guid? teamId = null, Guid? playerId = null) =>
        new EventBody(Guid.NewGuid(), type, teamId, playerId, clientTimestampUtc, sequenceNumber);

    private static Task<HttpResponseMessage> PostAsync(Scenario scenario, object[] events) =>
        scenario.Organizer.Client.PostAsJsonAsync($"/matches/{scenario.MatchId}/events", new { events });

    private static async Task<JsonElement> SendAsync(Scenario scenario, object[] events)
    {
        var response = await PostAsync(scenario, events);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> GetStateAsync(Scenario scenario)
    {
        using var document = JsonDocument.Parse(
            await scenario.Organizer.Client.GetStringAsync($"/matches/{scenario.MatchId}/state"));
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

        return new Scenario(organizer, tournamentId, stageId, homeTeamId, awayTeamId, matchId);
    }

    private sealed record EventBody(
        Guid id,
        string type,
        Guid? teamId,
        Guid? playerId,
        DateTime clientTimestampUtc,
        long sequenceNumber);

    private sealed record Scenario(
        TestOrganizer Organizer,
        Guid TournamentId,
        Guid StageId,
        Guid HomeTeamId,
        Guid AwayTeamId,
        Guid MatchId);
}
