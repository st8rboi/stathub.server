using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stathub.WebApi.IntegrationTests;

/// <summary>
/// Заявка команды с составом, скопированным из её заявки в другом турнире.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RosterCopyTests(StathubApiFactory factory)
{
    [Fact]
    public async Task AddTeam_WithCopySource_CopiesActivePlayers_AndSkipsBusyOnes()
    {
        var organizer = await factory.CreateOrganizerAsync();

        // Прошлый турнир: трое игроков, третий отзаявлен — его не копируем.
        var oldStageId = await organizer.CreateStageAsync(await organizer.CreateTournamentAsync());
        var oldDivisionId = organizer.GetDivisionId(oldStageId);
        var teamId = await organizer.CreateTeamAsync();
        var sourceId = await organizer.RegisterTeamAsync(oldStageId, teamId);

        var kept = await RegisterPlayerAsync(organizer, oldDivisionId, sourceId);
        var busy = await RegisterPlayerAsync(organizer, oldDivisionId, sourceId);
        var released = await RegisterPlayerAsync(organizer, oldDivisionId, sourceId);
        (await organizer.Client.PostAsync(
            $"/divisions/{oldDivisionId}/teams/{sourceId}/players/{released.RegistrationId}/release",
            null)).EnsureSuccessStatusCode();

        // Новый турнир: один из игроков уже заявлен за другую команду.
        var newStageId = await organizer.CreateStageAsync(await organizer.CreateTournamentAsync());
        var newDivisionId = organizer.GetDivisionId(newStageId);
        var otherEntryId = await organizer.RegisterTeamAsync(newStageId, await organizer.CreateTeamAsync());
        (await organizer.Client.PostAsJsonAsync(
            $"/divisions/{newDivisionId}/teams/{otherEntryId}/players",
            new { playerId = busy.PlayerId })).EnsureSuccessStatusCode();

        using (var sources = JsonDocument.Parse(await organizer.Client.GetStringAsync(
                   $"/divisions/{newDivisionId}/roster-sources?teamId={teamId}")))
        {
            var source = Assert.Single(sources.RootElement.EnumerateArray());
            Assert.Equal(sourceId, source.GetProperty("tournamentTeamId").GetGuid());
            Assert.Equal(2, source.GetProperty("activePlayers").GetInt32());
            Assert.Equal("Cup", source.GetProperty("tournamentName").GetString());
        }

        var added = await organizer.Client.PostAsJsonAsync(
            $"/divisions/{newDivisionId}/teams",
            new { teamId, copyFromTournamentTeamId = sourceId });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        using var result = JsonDocument.Parse(await added.Content.ReadAsStringAsync());
        Assert.Equal(1, result.RootElement.GetProperty("copiedPlayers").GetInt32());
        Assert.Equal(busy.PlayerId, Assert.Single(result.RootElement.GetProperty("skippedPlayerIds").EnumerateArray()).GetGuid());

        var entryId = result.RootElement.GetProperty("id").GetGuid();
        using var entry = JsonDocument.Parse(await organizer.Client.GetStringAsync(
            $"/divisions/{newDivisionId}/teams/{entryId}"));
        var registration = Assert.Single(entry.RootElement.GetProperty("registrations").EnumerateArray());
        Assert.Equal(kept.PlayerId, registration.GetProperty("playerId").GetGuid());
    }

    [Fact]
    public async Task RosterSources_OfOtherOrganizer_AreVisibleOnlyAfterPublish()
    {
        var owner = await factory.CreateOrganizerAsync();
        var ownerStageId = await owner.CreateStageAsync(await owner.CreateTournamentAsync());
        var teamId = await owner.CreateTeamAsync();
        var sourceId = await owner.RegisterTeamAsync(ownerStageId, teamId);
        await RegisterPlayerAsync(owner, owner.GetDivisionId(ownerStageId), sourceId);

        var stranger = await factory.CreateOrganizerAsync();
        var strangerDivisionId = stranger.GetDivisionId(
            await stranger.CreateStageAsync(await stranger.CreateTournamentAsync()));
        var sourcesUrl = $"/divisions/{strangerDivisionId}/roster-sources?teamId={teamId}";

        // Черновая лига чужого организатора не раскрывается ни в списке, ни при копировании.
        using (var hidden = JsonDocument.Parse(await stranger.Client.GetStringAsync(sourcesUrl)))
            Assert.Equal(0, hidden.RootElement.GetArrayLength());

        var denied = await stranger.Client.PostAsJsonAsync(
            $"/divisions/{strangerDivisionId}/teams",
            new { teamId, copyFromTournamentTeamId = sourceId });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);

        (await owner.Client.PostAsync($"/leagues/{owner.LeagueId}/publish", null)).EnsureSuccessStatusCode();

        using (var visible = JsonDocument.Parse(await stranger.Client.GetStringAsync(sourcesUrl)))
            Assert.Equal(1, visible.RootElement.GetArrayLength());
    }

    private static async Task<(Guid PlayerId, Guid RegistrationId)> RegisterPlayerAsync(
        TestOrganizer organizer,
        Guid divisionId,
        Guid entryId)
    {
        var playerId = await organizer.CreatePlayerAsync();
        var registrationId = await (await organizer.Client.PostAsJsonAsync(
            $"/divisions/{divisionId}/teams/{entryId}/players",
            new { playerId })).ReadIdAsync();

        return (playerId, registrationId);
    }
}
