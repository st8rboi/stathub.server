using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stathub.WebApi.IntegrationTests;

/// <summary>
/// Профиль игрока на витрине: выступления только в опубликованных лигах, статистика — из протокола.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PublicPlayerPageTests(StathubApiFactory factory)
{
    [Fact]
    public async Task PlayerInPublishedLeague_ReturnsCareerWithStatistics()
    {
        var organizer = await factory.CreateOrganizerAsync();
        var tournamentId = await organizer.CreateTournamentAsync();
        var stageId = await organizer.CreateStageAsync(tournamentId);
        var divisionId = organizer.GetDivisionId(stageId);

        (await organizer.Client.PostAsync($"/leagues/{organizer.LeagueId}/publish", null)).EnsureSuccessStatusCode();

        var homeTeamId = await organizer.CreateTeamAsync();
        var awayTeamId = await organizer.CreateTeamAsync();
        var homeEntryId = await organizer.RegisterTeamAsync(stageId, homeTeamId);
        await organizer.RegisterTeamAsync(stageId, awayTeamId);

        var playerId = await CreateRegisteredPlayerAsync(organizer, divisionId, homeEntryId, "Profile Scorer Patronymic");

        var now = DateTime.UtcNow;
        var matchId = await organizer.CreateMatchAsync(stageId, homeTeamId, awayTeamId);
        (await organizer.Client.PostAsJsonAsync($"/matches/{matchId}/events", new
        {
            events = new[]
            {
                Event("MatchStarted", null, null, now.AddMinutes(-30), 1),
                Event("Goal", homeTeamId, playerId, now.AddMinutes(-20), 2),
                Event("Goal", homeTeamId, playerId, now.AddMinutes(-10), 3),
            }
        })).EnsureSuccessStatusCode();

        var response = await factory.CreateClient().GetAsync($"/public/players/{playerId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Profile Scorer", json.RootElement.GetProperty("player").GetProperty("fullName").GetString());

        var career = json.RootElement.GetProperty("career");
        Assert.Equal(1, career.GetArrayLength());

        var row = career[0];
        Assert.Equal(tournamentId, row.GetProperty("tournamentId").GetGuid());
        Assert.Equal(homeTeamId, row.GetProperty("teamId").GetGuid());
        Assert.Equal(stageId, row.GetProperty("stageId").GetGuid());
        Assert.Equal("Active", row.GetProperty("status").GetString());
        Assert.Equal(1, row.GetProperty("matches").GetInt32());
        Assert.Equal(2, row.GetProperty("goals").GetInt32());

        // Протокол матча ссылается на игрока по Id и тоже без отчества.
        var match = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/public/matches/{matchId}");
        var goal = match.GetProperty("events").EnumerateArray().First(item =>
            item.GetProperty("playerId").ValueKind == JsonValueKind.String
            && item.GetProperty("playerId").GetGuid() == playerId);
        Assert.Equal("Profile Scorer", goal.GetProperty("playerName").GetString());

        // Состав команды — тоже без отчества.
        var team = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/public/teams/{homeTeamId}");
        Assert.Equal("Profile Scorer", team.GetProperty("players")[0].GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task PlayerOnlyInUnpublishedLeague_Returns404()
    {
        var organizer = await factory.CreateOrganizerAsync();
        var tournamentId = await organizer.CreateTournamentAsync();
        var stageId = await organizer.CreateStageAsync(tournamentId);
        var divisionId = organizer.GetDivisionId(stageId);

        var teamId = await organizer.CreateTeamAsync();
        var entryId = await organizer.RegisterTeamAsync(stageId, teamId);
        var playerId = await CreateRegisteredPlayerAsync(organizer, divisionId, entryId, "Hidden Player");

        var response = await factory.CreateClient().GetAsync($"/public/players/{playerId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<Guid> CreateRegisteredPlayerAsync(
        TestOrganizer organizer,
        Guid divisionId,
        Guid entryId,
        string fullName)
    {
        var playerId = await (await organizer.Client.PostAsJsonAsync("/players", new { fullName })).ReadIdAsync();

        (await organizer.Client.PostAsJsonAsync(
            $"/divisions/{divisionId}/teams/{entryId}/players",
            new { playerId })).EnsureSuccessStatusCode();

        return playerId;
    }

    private static object Event(string type, Guid? teamId, Guid? playerId, DateTime at, long sequence) => new
    {
        id = Guid.NewGuid(),
        type,
        teamId,
        playerId,
        relatedPlayerId = (Guid?)null,
        targetEventId = (Guid?)null,
        reason = (string?)null,
        clientTimestampUtc = at,
        sequenceNumber = sequence
    };
}
