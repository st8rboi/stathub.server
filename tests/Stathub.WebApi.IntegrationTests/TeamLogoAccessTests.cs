using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Stathub.Modules.Teams.Application;
using Xunit;

namespace Stathub.WebApi.IntegrationTests;

/// <summary>Логотип закреплён за первой загрузившей организацией; снять закрепление может техподдержка.</summary>
[Collection(ApiCollection.Name)]
public sealed class TeamLogoAccessTests(StathubApiFactory factory)
{
    [Fact]
    public async Task OtherOrganizer_CannotReplaceOrDeleteLogo_ButOwnerCan()
    {
        var owner = await factory.CreateOrganizerAsync();
        var other = await factory.CreateOrganizerAsync();
        var teamId = await owner.CreateTeamAsync();

        var first = await UploadAsync(owner.Client, teamId);
        var firstLogo = await first.ReadStringAsync("logoUrl");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(other.Client, teamId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.Client.DeleteAsync($"/teams/{teamId}/logo")).StatusCode);

        // Логотип не изменился, команда остаётся доступной другому организатору для чтения.
        var seen = await other.Client.GetAsync($"/teams/{teamId}");
        Assert.Equal(firstLogo, await seen.ReadStringAsync("logoUrl"));

        var replaced = await UploadAsync(owner.Client, teamId);
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        Assert.NotEqual(firstLogo, await replaced.ReadStringAsync("logoUrl"));
    }

    [Fact]
    public async Task AnyOrganizer_CanUploadToTeamCreatedByOthers_WhenNoLogoYet()
    {
        var creator = await factory.CreateOrganizerAsync();
        var other = await factory.CreateOrganizerAsync();
        var teamId = await creator.CreateTeamAsync();

        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(other.Client, teamId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(creator.Client, teamId)).StatusCode);
    }

    [Fact]
    public async Task SupportReset_ReleasesLogo()
    {
        var owner = await factory.CreateOrganizerAsync();
        var other = await factory.CreateOrganizerAsync();
        var teamId = await owner.CreateTeamAsync();

        await UploadAsync(owner.Client, teamId);

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<TeamService>().ResetLogoAsync(teamId);

        var response = await UploadAsync(other.Client, teamId);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotEqual(JsonValueKind.Null, document.RootElement.GetProperty("logoOwnerTenantId").ValueKind);
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid teamId)
    {
        var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        return client.PostAsync(
            $"/teams/{teamId}/logo",
            new MultipartFormDataContent { { file, "file", "logo.png" } });
    }
}
