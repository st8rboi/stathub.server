using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stathub.WebApi.IntegrationTests;

/// <summary>
/// Приём офлайн-очереди судьи: очередь не должна блокироваться, повторы безопасны,
/// статус матча идёт теми же событиями, что и голы.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class OfflineIntakeTests(StathubApiFactory factory)
{
    [Fact]
    public async Task WholeOfflineMatch_InOneBatch_EndsFinishedWithLastGoalCounted()
    {
        var scenario = await CreateScenarioAsync();
        var kickOff = DateTime.UtcNow.AddMinutes(-100);

        var result = await SendAsync(scenario, [
            Event("MatchStarted", 1, kickOff),
            Event("Goal", 2, kickOff.AddMinutes(30), scenario.HomeTeamId),
            Event("PeriodStarted", 3, kickOff.AddMinutes(50)),
            Event("Goal", 4, kickOff.AddMinutes(95), scenario.HomeTeamId),
            Event("MatchFinished", 5, kickOff.AddMinutes(96))
        ]);

        var match = result.GetProperty("state").GetProperty("match");

        Assert.Equal(5, result.GetProperty("acceptedEventIds").GetArrayLength());
        Assert.Equal("Finished", match.GetProperty("status").GetString());
        Assert.Equal(2, match.GetProperty("homeScore").GetInt32());
        Assert.Equal(2, match.GetProperty("currentPeriod").GetInt32());
    }

    [Fact]
    public async Task BatchOrder_IsTakenFromSequenceNumber_NotFromArrival()
    {
        var scenario = await CreateScenarioAsync();
        var now = DateTime.UtcNow;

        var result = await SendAsync(scenario, [
            Event("MatchFinished", 2, now),
            Event("MatchStarted", 1, now.AddMinutes(-90))
        ]);

        Assert.Equal(0, result.GetProperty("rejectedEvents").GetArrayLength());
        Assert.Equal("Finished", result.GetProperty("state").GetProperty("match").GetProperty("status").GetString());
    }

    [Fact]
    public async Task RetryAfterLostResponse_OfFinishedMatch_IsNotAnError()
    {
        var scenario = await CreateScenarioAsync();
        var now = DateTime.UtcNow;

        object[] batch = [Event("MatchStarted", 1, now.AddMinutes(-5)), Event("MatchFinished", 2, now)];

        await SendAsync(scenario, batch);
        var retry = await SendAsync(scenario, batch);

        Assert.Equal(0, retry.GetProperty("acceptedEventIds").GetArrayLength());
        Assert.Equal(2, retry.GetProperty("duplicateEventIds").GetArrayLength());
        Assert.Equal(0, retry.GetProperty("rejectedEvents").GetArrayLength());
    }

    [Fact]
    public async Task ImpossibleEvent_IsRejectedAlone_AndDoesNotBlockTheQueue()
    {
        var scenario = await CreateScenarioAsync();
        var now = DateTime.UtcNow;

        var bad = Event("Goal", 1, now, Guid.NewGuid());
        var good = Event("Goal", 2, now, scenario.AwayTeamId);

        var result = await SendAsync(scenario, [bad, good]);

        var rejected = Assert.Single(result.GetProperty("rejectedEvents").EnumerateArray());
        Assert.Equal(bad.id, rejected.GetProperty("eventId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(rejected.GetProperty("reason").GetString()));

        Assert.Equal(good.id, Assert.Single(result.GetProperty("acceptedEventIds").EnumerateArray()).GetGuid());
        Assert.Equal(1, result.GetProperty("state").GetProperty("match").GetProperty("awayScore").GetInt32());
    }

    [Fact]
    public async Task InvalidTransition_AndEventAfterFinish_AreRejected_NotBadRequest()
    {
        var scenario = await CreateScenarioAsync();
        var now = DateTime.UtcNow;

        var pauseBeforeStart = await SendAsync(scenario, [Event("MatchPaused", 1, now)]);
        Assert.Single(pauseBeforeStart.GetProperty("rejectedEvents").EnumerateArray());

        await SendAsync(scenario, [Event("MatchStarted", 2, now), Event("MatchFinished", 3, now)]);

        var lateGoal = await SendAsync(scenario, [Event("Goal", 4, now, scenario.HomeTeamId)]);
        Assert.Single(lateGoal.GetProperty("rejectedEvents").EnumerateArray());
        Assert.Equal(0, lateGoal.GetProperty("state").GetProperty("match").GetProperty("homeScore").GetInt32());
    }

    [Fact]
    public async Task GoalOfDisqualifiedPlayer_IsCounted_AndFlaggedForOrganizer_UntilVoided()
    {
        var scenario = await CreateScenarioAsync();
        var playerId = await scenario.Organizer.CreatePlayerAsync();
        var now = DateTime.UtcNow;

        await (await scenario.Organizer.Client.PostAsJsonAsync(
            $"/discipline/tournaments/{scenario.TournamentId}",
            new { teamId = scenario.HomeTeamId, playerId, reason = "Red card", matchesBanned = 2 })).ReadIdAsync();

        var goal = new
        {
            id = Guid.NewGuid(),
            type = "Goal",
            teamId = (Guid?)scenario.HomeTeamId,
            playerId = (Guid?)playerId,
            targetEventId = (Guid?)null,
            clientTimestampUtc = now,
            sequenceNumber = 1L
        };

        var result = await SendAsync(scenario, [goal]);
        var state = result.GetProperty("state");

        Assert.Equal(1, state.GetProperty("match").GetProperty("homeScore").GetInt32());
        Assert.Equal(1, state.GetProperty("match").GetProperty("eventsForReview").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(state.GetProperty("events")[0].GetProperty("reviewNote").GetString()));

        var voided = await SendAsync(scenario, [
            new
            {
                id = Guid.NewGuid(),
                type = "EventVoided",
                teamId = (Guid?)null,
                playerId = (Guid?)null,
                targetEventId = (Guid?)goal.id,
                clientTimestampUtc = now,
                sequenceNumber = 2L
            }
        ]);

        Assert.Equal(0, voided.GetProperty("state").GetProperty("match").GetProperty("eventsForReview").GetInt32());
    }

    [Fact]
    public async Task EventTime_IsCorrectedByDeviceClockError_EvenWhenSentLate()
    {
        var scenario = await CreateScenarioAsync();

        // Часы телефона спешат на час; событие случилось за 40 минут до отправки.
        var deviceNow = DateTime.UtcNow.AddHours(1);
        var happenedOnDevice = deviceNow.AddMinutes(-40);

        var result = await SendAsync(scenario, [Event("MatchStarted", 1, happenedOnDevice)], sentAtUtc: deviceNow);

        var startedAt = result.GetProperty("state").GetProperty("match").GetProperty("startedAtUtc").GetDateTime();
        var expected = DateTime.UtcNow.AddMinutes(-40);

        Assert.InRange((startedAt.ToUniversalTime() - expected).Duration(), TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Pauses_AreAccumulatedForTheClock_AndResetByNextPeriod()
    {
        var scenario = await CreateScenarioAsync();
        var start = DateTime.UtcNow.AddMinutes(-30);

        var paused = await SendAsync(scenario, [
            Event("MatchStarted", 1, start),
            Event("MatchPaused", 2, start.AddMinutes(5)),
            Event("MatchResumed", 3, start.AddMinutes(8))
        ]);

        var match = paused.GetProperty("state").GetProperty("match");
        Assert.InRange(match.GetProperty("periodPausedMilliseconds").GetInt64(), 179_000, 181_000);
        Assert.Equal(JsonValueKind.Null, match.GetProperty("pausedAtUtc").ValueKind);

        var nextPeriod = await SendAsync(scenario, [Event("PeriodStarted", 4, start.AddMinutes(10))]);

        Assert.Equal(0, nextPeriod.GetProperty("state").GetProperty("match").GetProperty("periodPausedMilliseconds").GetInt64());
    }

    [Fact]
    public async Task LifecycleEvent_CannotBeVoided()
    {
        var scenario = await CreateScenarioAsync();
        var now = DateTime.UtcNow;
        var start = Event("MatchStarted", 1, now);

        await SendAsync(scenario, [start]);

        var result = await SendAsync(scenario, [
            new
            {
                id = Guid.NewGuid(),
                type = "EventVoided",
                teamId = (Guid?)null,
                targetEventId = (Guid?)start.id,
                clientTimestampUtc = now,
                sequenceNumber = 2L
            }
        ]);

        Assert.Single(result.GetProperty("rejectedEvents").EnumerateArray());
    }

    [Fact]
    public async Task PublicProtocol_ShowsPeriodsAndFinalWhistle_ButNotPausesOrReviewNotes()
    {
        var scenario = await CreateScenarioAsync();
        var now = DateTime.UtcNow;

        (await scenario.Organizer.Client.PostAsync($"/leagues/{scenario.Organizer.LeagueId}/publish", null))
            .EnsureSuccessStatusCode();

        await SendAsync(scenario, [
            Event("MatchStarted", 1, now.AddMinutes(-20)),
            Event("MatchPaused", 2, now.AddMinutes(-15)),
            Event("MatchResumed", 3, now.AddMinutes(-14)),
            Event("PeriodStarted", 4, now.AddMinutes(-10)),
            Event("MatchFinished", 5, now)
        ]);

        var response = await factory.CreateClient().GetAsync($"/public/matches/{scenario.MatchId}");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        var types = document.RootElement.GetProperty("events").EnumerateArray()
            .Select(item => item.GetProperty("type").GetString())
            .ToList();

        Assert.Equal(["MatchStarted", "PeriodStarted", "MatchFinished"], types);
        Assert.DoesNotContain("reviewNote", body);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("stop")]
    [InlineData("resume")]
    [InlineData("next-period")]
    [InlineData("finish")]
    public async Task OldStatusEndpoints_AreGone(string action)
    {
        var scenario = await CreateScenarioAsync();

        var response = await scenario.Organizer.Client.PostAsync($"/matches/{scenario.MatchId}/{action}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static EventBody Event(string type, long sequenceNumber, DateTime clientTimestampUtc, Guid? teamId = null) =>
        new(Guid.NewGuid(), type, teamId, clientTimestampUtc, sequenceNumber);

    private static async Task<JsonElement> SendAsync(Scenario scenario, object[] events, DateTime? sentAtUtc = null)
    {
        var response = await scenario.Organizer.Client.PostAsJsonAsync(
            $"/matches/{scenario.MatchId}/events",
            new { events, sentAtUtc });

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

        return new Scenario(organizer, tournamentId, homeTeamId, awayTeamId, matchId);
    }

    private sealed record EventBody(
        Guid id,
        string type,
        Guid? teamId,
        DateTime clientTimestampUtc,
        long sequenceNumber);

    private sealed record Scenario(
        TestOrganizer Organizer,
        Guid TournamentId,
        Guid HomeTeamId,
        Guid AwayTeamId,
        Guid MatchId);
}
