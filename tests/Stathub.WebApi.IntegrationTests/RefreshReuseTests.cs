using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Stathub.Modules.Identity.Application;
using Xunit;

namespace Stathub.WebApi.IntegrationTests;

/// <summary>Потерянный ответ на обновление сессии не должен разлогинивать судью посреди матча.</summary>
[Collection(ApiCollection.Name)]
public sealed class RefreshReuseTests(StathubApiFactory factory)
{
    [Fact]
    public async Task RotatedToken_CanBeRepeated_InShortWindow()
    {
        var session = await LoginAsync();

        var first = await RefreshAsync(session.Token);
        var repeated = await RefreshAsync(session.Token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
    }

    [Fact]
    public async Task UnknownToken_IsRejected()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await RefreshAsync("unknown-token")).StatusCode);
    }

    [Fact]
    public async Task LoggedOutToken_IsNotAccepted()
    {
        var session = await LoginAsync();

        (await factory.CreateClient().PostAsJsonAsync("/auth/logout", new { refreshToken = session.Token }))
            .EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await RefreshAsync(session.Token)).StatusCode);
    }

    [Fact]
    public async Task LogoutOfRotatedToken_ClosesItsWindow()
    {
        var session = await LoginAsync();

        (await RefreshAsync(session.Token)).EnsureSuccessStatusCode();

        (await factory.CreateClient().PostAsJsonAsync("/auth/logout", new { refreshToken = session.Token }))
            .EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await RefreshAsync(session.Token)).StatusCode);
    }

    [Fact]
    public async Task PasswordChange_ClosesWindowOfRotatedToken()
    {
        var session = await LoginAsync();

        var refreshed = await RefreshAsync(session.Token);
        var accessToken = await refreshed.ReadStringAsync("accessToken");

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);

        (await client.PostAsJsonAsync("/auth/change-password", new
        {
            currentPassword = StathubApiFactory.Password,
            newPassword = "NewSecret12345"
        })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await RefreshAsync(session.Token)).StatusCode);
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        factory.CreateClient().PostAsJsonAsync("/auth/refresh", new { refreshToken });

    private async Task<(string Login, string Token)> LoginAsync()
    {
        var login = $"sess-{Guid.NewGuid():N}"[..20];

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<AccountService>()
                .CreateTenantWithOwnerAsync($"Org {login}", login, login, "User", StathubApiFactory.Password);
        }

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/auth/login",
            new { login, password = StathubApiFactory.Password });

        response.EnsureSuccessStatusCode();

        return (login, await response.ReadStringAsync("refreshToken"));
    }
}
