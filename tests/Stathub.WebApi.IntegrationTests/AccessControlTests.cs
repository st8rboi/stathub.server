using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stathub.WebApi.IntegrationTests;

/// <summary>Права судьи и изоляция организаций на маршрутах, где Id приходит из URL или тела запроса.</summary>
[Collection(ApiCollection.Name)]
public sealed class AccessControlTests(StathubApiFactory factory)
{
    [Fact]
    public async Task Referee_CannotCreateMatch_EvenOnOwnStage()
    {
        var scenario = await CreateScenarioAsync();

        var response = await scenario.Referee.PostAsJsonAsync("/matches", new
        {
            stageId = scenario.StageId,
            homeTeamId = scenario.HomeTeamId,
            awayTeamId = scenario.AwayTeamId,
            startTimeUtc = "2026-05-11T12:00:00Z"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Referee_CannotCreateMatch_OnOtherTenantStage()
    {
        var scenario = await CreateScenarioAsync();
        var stranger = await CreateScenarioAsync();

        var response = await scenario.Referee.PostAsJsonAsync("/matches", new
        {
            stageId = stranger.StageId,
            homeTeamId = stranger.HomeTeamId,
            awayTeamId = stranger.AwayTeamId,
            startTimeUtc = "2026-05-11T12:00:00Z"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organizer_CreatesMatch_OnOwnStage_ButNotOnOtherTenantStage()
    {
        var scenario = await CreateScenarioAsync();
        var stranger = await CreateScenarioAsync();

        var own = await scenario.Organizer.Client.PostAsJsonAsync("/matches", new
        {
            stageId = scenario.StageId,
            homeTeamId = scenario.HomeTeamId,
            awayTeamId = scenario.AwayTeamId,
            startTimeUtc = "2026-05-11T12:00:00Z"
        });

        var foreign = await scenario.Organizer.Client.PostAsJsonAsync("/matches", new
        {
            stageId = stranger.StageId,
            homeTeamId = stranger.HomeTeamId,
            awayTeamId = stranger.AwayTeamId,
            startTimeUtc = "2026-05-11T12:00:00Z"
        });

        Assert.Equal(HttpStatusCode.Created, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task Referee_ReadsRoster_ButCannotChangeIt()
    {
        var scenario = await CreateScenarioAsync();
        var playerId = await scenario.Organizer.CreatePlayerAsync();
        var newTeamId = await scenario.Organizer.CreateTeamAsync();
        var referee = scenario.Referee;
        var stage = $"/stages/{scenario.StageId}";

        Assert.Equal(HttpStatusCode.OK, (await referee.GetAsync($"{stage}/teams")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await referee.GetAsync($"{stage}/teams/{scenario.HomeRegistrationId}")).StatusCode);

        var writes = new[]
        {
            await referee.PostAsJsonAsync($"{stage}/teams", new { teamId = newTeamId }),
            await referee.PostAsJsonAsync(
                $"{stage}/teams/{scenario.HomeRegistrationId}/players",
                new { playerId }),
            await referee.PostAsync(
                $"{stage}/teams/{scenario.HomeRegistrationId}/players/{Guid.NewGuid()}/release",
                null),
            await referee.PostAsJsonAsync($"{stage}/transfers", new
            {
                playerId,
                fromTeamId = scenario.HomeTeamId,
                toTeamId = scenario.AwayTeamId
            }),
            await referee.PostAsync($"{stage}/teams/{scenario.HomeRegistrationId}/withdraw", null)
        };

        Assert.All(writes, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
    }

    [Fact]
    public async Task Organizer_CannotChangeOtherTenantRoster()
    {
        var scenario = await CreateScenarioAsync();
        var stranger = await factory.CreateOrganizerAsync();

        var response = await stranger.Client.PostAsync(
            $"/stages/{scenario.StageId}/teams/{scenario.HomeRegistrationId}/withdraw",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Coach_CannotListStageMatches()
    {
        var scenario = await CreateScenarioAsync();
        var coach = await scenario.Organizer.CreateStaffAsync("Coach");

        var response = await coach.GetAsync($"/matches?stageId={scenario.StageId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Referee_SeesOnlyAssignedMatches()
    {
        var scenario = await CreateScenarioAsync();
        await scenario.Organizer.CreateMatchAsync(scenario.StageId, scenario.HomeTeamId, scenario.AwayTeamId);

        var response = await scenario.Referee.GetAsync($"/matches?stageId={scenario.StageId}");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var match = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(scenario.MatchId, match.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Organizer_CannotPayOrCancelOtherTenantCharge()
    {
        var scenario = await CreateScenarioAsync();
        var client = scenario.Organizer.Client;

        (await client.PutAsJsonAsync($"/finance/tournaments/{scenario.TournamentId}/fee", new { amount = 1000 }))
            .EnsureSuccessStatusCode();

        await scenario.Organizer.CreateMatchAsync(scenario.StageId, scenario.HomeTeamId, scenario.AwayTeamId);

        var charges = await client.GetAsync($"/finance/stages/{scenario.StageId}/charges");
        charges.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await charges.Content.ReadAsStringAsync());
        var chargeId = document.RootElement[0].GetProperty("id").GetGuid();

        var stranger = await factory.CreateOrganizerAsync();

        var pay = await stranger.Client.PostAsJsonAsync($"/finance/charges/{chargeId}/pay", new { method = "Cash" });
        var cancel = await stranger.Client.PostAsJsonAsync($"/finance/charges/{chargeId}/cancel", new { comment = "x" });
        var ownCancel = await client.PostAsJsonAsync($"/finance/charges/{chargeId}/cancel", new { comment = "ok" });

        Assert.Equal(HttpStatusCode.NotFound, pay.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ownCancel.StatusCode);
    }

    [Fact]
    public async Task Organizer_CannotLiftOtherTenantDisqualification()
    {
        var scenario = await CreateScenarioAsync();
        var playerId = await scenario.Organizer.CreatePlayerAsync();

        var disqualificationId = await (await scenario.Organizer.Client.PostAsJsonAsync(
            $"/discipline/tournaments/{scenario.TournamentId}",
            new { teamId = scenario.HomeTeamId, playerId, reason = "Red card", matchesBanned = 1 })).ReadIdAsync();

        var stranger = await factory.CreateOrganizerAsync();

        var foreign = await stranger.Client.PostAsJsonAsync(
            $"/discipline/disqualifications/{disqualificationId}/lift",
            new { comment = "x" });

        var own = await scenario.Organizer.Client.PostAsJsonAsync(
            $"/discipline/disqualifications/{disqualificationId}/lift",
            new { comment = "ok" });

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, own.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotGrantTenantWideRole_ThroughAssignments()
    {
        var scenario = await CreateScenarioAsync();
        var admin = await scenario.Organizer.CreateStaffAsync("Admin");
        var adminId = await (await admin.GetAsync("/auth/me")).ReadStringAsync("id");

        // Назначение со скоупом Tenant сделало бы администратора организатором.
        var asOrganizer = await admin.PostAsJsonAsync("/accounts/assignments", new
        {
            userId = adminId,
            role = "Organizer",
            scopeType = "Tenant"
        });

        var tenantWide = await admin.PostAsJsonAsync("/accounts/assignments", new
        {
            userId = adminId,
            role = "Admin",
            scopeType = "Tenant"
        });

        Assert.Equal(HttpStatusCode.BadRequest, asOrganizer.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tenantWide.StatusCode);
        Assert.Equal("Admin", await (await admin.GetAsync("/auth/me")).ReadStringAsync("role"));
    }

    [Fact]
    public async Task RepeatedMatchAssignment_ReturnsSameAssignment()
    {
        var scenario = await CreateScenarioAsync();
        var refereeId = await (await scenario.Referee.GetAsync("/auth/me")).ReadStringAsync("id");

        var body = new
        {
            userId = refereeId,
            role = "Referee",
            scopeType = "Match",
            scopeId = scenario.MatchId
        };

        // Судья уже назначен на матч в сценарии: повтор не должен создавать второе назначение.
        var first = await scenario.Organizer.Client.PostAsJsonAsync("/accounts/assignments", body);
        var second = await scenario.Organizer.Client.PostAsJsonAsync("/accounts/assignments", body);

        Assert.Equal(await first.ReadIdAsync(), await second.ReadIdAsync());

        using var list = JsonDocument.Parse(
            await scenario.Organizer.Client.GetStringAsync("/accounts/assignments"));

        Assert.Single(
            list.RootElement.EnumerateArray(),
            item => item.GetProperty("scopeType").GetString() == "Match"
                    && item.GetProperty("scopeId").GetGuid() == scenario.MatchId);
    }

    private async Task<Scenario> CreateScenarioAsync()
    {
        var organizer = await factory.CreateOrganizerAsync();

        var tournamentId = await organizer.CreateTournamentAsync();
        var stageId = await organizer.CreateStageAsync(tournamentId);

        var homeTeamId = await organizer.CreateTeamAsync();
        var awayTeamId = await organizer.CreateTeamAsync();

        var homeRegistrationId = await organizer.RegisterTeamAsync(stageId, homeTeamId);
        await organizer.RegisterTeamAsync(stageId, awayTeamId);

        var matchId = await organizer.CreateMatchAsync(stageId, homeTeamId, awayTeamId);
        var referee = await organizer.CreateStaffAsync("Referee", matchId);

        return new Scenario(
            organizer,
            referee,
            tournamentId,
            stageId,
            homeTeamId,
            awayTeamId,
            homeRegistrationId,
            matchId);
    }

    private sealed record Scenario(
        TestOrganizer Organizer,
        HttpClient Referee,
        Guid TournamentId,
        Guid StageId,
        Guid HomeTeamId,
        Guid AwayTeamId,
        Guid HomeRegistrationId,
        Guid MatchId);
}
