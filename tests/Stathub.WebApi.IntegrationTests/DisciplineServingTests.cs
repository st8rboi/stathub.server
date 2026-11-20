using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stathub.WebApi.IntegrationTests;

/// <summary>
/// Отбытие дисквалификации считается по матчам, доигранным после её выдачи (по финальному
/// свистку), а не по расписанию: матч с красной карточкой в срок не входит.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DisciplineServingTests(StathubApiFactory factory)
{
    [Fact]
    public async Task BanIssuedAfterTheMatch_IsNotServedByThatMatch()
    {
        var scenario = await CreateScenarioAsync();
        var kickOff = DateTime.UtcNow.AddMinutes(-100);

        // Матч с красной сыгран сегодня, дисквалификацию выдали вечером после него.
        var today = await CreateMatchAtAsync(scenario, kickOff);
        await PlayAsync(scenario, today, kickOff);
        await IssueAsync(scenario, matchesBanned: 1);

        var ban = await GetSingleBanAsync(scenario);

        Assert.Equal(0, ban.GetProperty("matchesServed").GetInt32());
        Assert.Equal(1, ban.GetProperty("matchesLeft").GetInt32());
        Assert.True(ban.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task PostponedMatch_PlayedAfterTheBan_IsServed()
    {
        var scenario = await CreateScenarioAsync();

        // Матч фабрики стоит в расписании задолго до выдачи (перенесён), а доигран после неё.
        await IssueAsync(scenario, matchesBanned: 1);
        await PlayAsync(scenario, scenario.MatchId, DateTime.UtcNow.AddMinutes(-1), finishedAtUtc: DateTime.UtcNow);

        var ban = await GetSingleBanAsync(scenario);

        Assert.Equal(1, ban.GetProperty("matchesServed").GetInt32());
        Assert.False(ban.GetProperty("isActive").GetBoolean());
    }

    private static async Task PlayAsync(Scenario scenario, Guid matchId, DateTime kickOff, DateTime? finishedAtUtc = null)
    {
        var response = await scenario.Organizer.Client.PostAsJsonAsync($"/matches/{matchId}/events", new
        {
            events = new object[]
            {
                new { id = Guid.NewGuid(), type = "MatchStarted", clientTimestampUtc = kickOff, sequenceNumber = 1L },
                new { id = Guid.NewGuid(), type = "MatchFinished", clientTimestampUtc = finishedAtUtc ?? kickOff.AddMinutes(95), sequenceNumber = 2L }
            }
        });

        response.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> CreateMatchAtAsync(Scenario scenario, DateTime startTimeUtc) =>
        await (await scenario.Organizer.Client.PostAsJsonAsync("/matches", new
        {
            stageId = scenario.StageId,
            homeTeamId = scenario.HomeTeamId,
            awayTeamId = scenario.AwayTeamId,
            startTimeUtc
        })).ReadIdAsync();

    private static async Task IssueAsync(Scenario scenario, int matchesBanned) =>
        await (await scenario.Organizer.Client.PostAsJsonAsync(
            $"/discipline/tournaments/{scenario.TournamentId}",
            new { teamId = scenario.HomeTeamId, playerId = scenario.PlayerId, reason = "Red card", matchesBanned })).ReadIdAsync();

    private static async Task<JsonElement> GetSingleBanAsync(Scenario scenario)
    {
        using var document = JsonDocument.Parse(
            await scenario.Organizer.Client.GetStringAsync($"/discipline/tournaments/{scenario.TournamentId}"));

        return Assert.Single(document.RootElement.EnumerateArray()).Clone();
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

        // Матчи фабрики стоят в расписании на 2026-05-10 — заведомо раньше выдачи.
        var matchId = await organizer.CreateMatchAsync(stageId, homeTeamId, awayTeamId);
        var playerId = await organizer.CreatePlayerAsync();

        return new Scenario(organizer, tournamentId, stageId, homeTeamId, awayTeamId, matchId, playerId);
    }

    private sealed record Scenario(
        TestOrganizer Organizer,
        Guid TournamentId,
        Guid StageId,
        Guid HomeTeamId,
        Guid AwayTeamId,
        Guid MatchId,
        Guid PlayerId);
}
