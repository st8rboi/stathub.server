using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Stathub.Modules.Identity.Application;
using Stathub.WebApi.Infrastructure.Auth;
using Xunit;

namespace Stathub.WebApi.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class LoginThrottleTests(StathubApiFactory factory)
{
    private const string WrongPassword = "WrongPassword1";

    [Fact]
    public async Task AfterMaxFailures_LoginIsBlocked_EvenWithCorrectPassword()
    {
        var login = await CreateUserAsync();

        for (var attempt = 0; attempt < LoginThrottle.MaxFailures; attempt++)
            Assert.Equal(HttpStatusCode.BadRequest, (await TryLoginAsync(login, WrongPassword)).StatusCode);

        var blocked = await TryLoginAsync(login, StathubApiFactory.Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter);
    }

    [Fact]
    public async Task BlockOfOneLogin_DoesNotAffectAnother()
    {
        var blockedLogin = await CreateUserAsync();
        var otherLogin = await CreateUserAsync();

        for (var attempt = 0; attempt < LoginThrottle.MaxFailures; attempt++)
            await TryLoginAsync(blockedLogin, WrongPassword);

        Assert.Equal(HttpStatusCode.OK, (await TryLoginAsync(otherLogin, StathubApiFactory.Password)).StatusCode);
    }

    [Fact]
    public async Task SuccessfulLogin_ResetsFailures()
    {
        var login = await CreateUserAsync();

        for (var round = 0; round < 2; round++)
        {
            for (var attempt = 0; attempt < LoginThrottle.MaxFailures - 1; attempt++)
                await TryLoginAsync(login, WrongPassword);

            Assert.Equal(HttpStatusCode.OK, (await TryLoginAsync(login, StathubApiFactory.Password)).StatusCode);
        }
    }

    [Fact]
    public async Task LoginCaseAndSpaces_DoNotBypassBlock()
    {
        var login = await CreateUserAsync();

        for (var attempt = 0; attempt < LoginThrottle.MaxFailures; attempt++)
            await TryLoginAsync(login, WrongPassword);

        var blocked = await TryLoginAsync($"  {login.ToUpperInvariant()} ", StathubApiFactory.Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task IpLimit_RejectsRequestsAcrossDifferentLogins()
    {
        await using var limited = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:LoginIpLimitPerMinute", "2"));

        var client = limited.CreateClient();

        async Task<HttpStatusCode> AttemptAsync() =>
            (await client.PostAsJsonAsync("/auth/login", new
            {
                login = $"missing-{Guid.NewGuid():N}",
                password = WrongPassword
            })).StatusCode;

        Assert.Equal(HttpStatusCode.BadRequest, await AttemptAsync());
        Assert.Equal(HttpStatusCode.BadRequest, await AttemptAsync());
        Assert.Equal(HttpStatusCode.TooManyRequests, await AttemptAsync());
    }

    private Task<HttpResponseMessage> TryLoginAsync(string login, string password) =>
        factory.CreateClient().PostAsJsonAsync("/auth/login", new { login, password });

    private async Task<string> CreateUserAsync()
    {
        var login = $"user-{Guid.NewGuid():N}"[..20];

        using var scope = factory.Services.CreateScope();

        await scope.ServiceProvider
            .GetRequiredService<AccountService>()
            .CreateTenantWithOwnerAsync($"Org {login}", login, login, "User", StathubApiFactory.Password);

        return login;
    }
}
