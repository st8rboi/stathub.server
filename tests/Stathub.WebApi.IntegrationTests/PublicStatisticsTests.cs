using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stathub.WebApi.IntegrationTests;

/// <summary>
/// Статистика витрины на реальном объёме: в дивизионе легко больше 100 игроков с событиями,
/// а полный список отдаётся файлом .xlsx.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PublicStatisticsTests(StathubApiFactory factory)
{
    private const int PlayersPerTeam = 55;

    [Fact]
    public async Task DivisionWithMoreThan100Scorers_ReturnsStatistics_AndFullXlsx()
    {
        var (tournamentId, scorers) = await CreateScoringTournamentAsync();
        var anonymous = factory.CreateClient();

        var statistics = await anonymous.GetAsync($"/public/tournaments/{tournamentId}/statistics");
        Assert.Equal(HttpStatusCode.OK, statistics.StatusCode);

        using (var json = JsonDocument.Parse(await statistics.Content.ReadAsStringAsync()))
            Assert.Equal(scorers, json.RootElement.GetProperty("rows").GetArrayLength());

        var file = await anonymous.GetAsync($"/public/tournaments/{tournamentId}/statistics.xlsx");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            file.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith(".xlsx", file.Content.Headers.ContentDisposition?.FileNameStar);

        await using var stream = await file.Content.ReadAsStreamAsync();
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry("xl/workbook.xml"));

        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var sheet = await reader.ReadToEndAsync();

        // Все бомбардиры в файле, без ссылок на фото и логотипы.
        Assert.Contains($"Scorer {scorers - 1}", sheet);
        Assert.DoesNotContain("/uploads/", sheet);
    }

    private async Task<(Guid TournamentId, int Scorers)> CreateScoringTournamentAsync()
    {
        var organizer = await factory.CreateOrganizerAsync();
        var tournamentId = await organizer.CreateTournamentAsync();
        var stageId = await organizer.CreateStageAsync(tournamentId);
        var divisionId = organizer.GetDivisionId(stageId);

        (await organizer.Client.PostAsync($"/leagues/{organizer.LeagueId}/publish", null)).EnsureSuccessStatusCode();

        var homeTeamId = await organizer.CreateTeamAsync();
        var awayTeamId = await organizer.CreateTeamAsync();
        var homeEntryId = await organizer.RegisterTeamAsync(stageId, homeTeamId);
        var awayEntryId = await organizer.RegisterTeamAsync(stageId, awayTeamId);

        var goals = new List<object>();
        var now = DateTime.UtcNow;
        var sequence = 1L;
        goals.Add(Event("MatchStarted", null, null, now.AddMinutes(-60), sequence++));

        var index = 0;
        foreach (var (teamId, entryId) in new[] { (homeTeamId, homeEntryId), (awayTeamId, awayEntryId) })
        {
            for (var number = 0; number < PlayersPerTeam; number++, index++)
            {
                var playerId = await (await organizer.Client.PostAsJsonAsync(
                    "/players",
                    new { fullName = $"Scorer {index}" })).ReadIdAsync();

                (await organizer.Client.PostAsJsonAsync(
                    $"/divisions/{divisionId}/teams/{entryId}/players",
                    new { playerId })).EnsureSuccessStatusCode();

                goals.Add(Event("Goal", teamId, playerId, now.AddMinutes(-59).AddSeconds(index), sequence++));
            }
        }

        var matchId = await organizer.CreateMatchAsync(stageId, homeTeamId, awayTeamId);
        var appended = await organizer.Client.PostAsJsonAsync($"/matches/{matchId}/events", new { events = goals });
        appended.EnsureSuccessStatusCode();

        return (tournamentId, index);
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
