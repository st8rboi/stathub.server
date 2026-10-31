using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Stathub.Modules.Identity.Application;
using Xunit;

namespace Stathub.WebApi.IntegrationTests;

/// <summary>
/// Настоящее приложение на отдельной базе <c>stathub_test</c>: она пересоздаётся при каждом запуске,
/// рабочая dev-база не затрагивается. Подключение — переменная STATHUB_TEST_POSTGRES либо
/// user secrets WebApi; имя базы из них всегда заменяется на тестовое.
/// </summary>
public sealed class StathubApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Secret12345";

    private const string DatabaseName = "stathub_test";

    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"stathub-tests-{Guid.NewGuid():N}");
    private readonly string _connectionString = BuildConnectionString();

    public async Task InitializeAsync()
    {
        var maintenance = new NpgsqlConnectionStringBuilder(_connectionString) { Database = "postgres" };

        await using var connection = new NpgsqlConnection(maintenance.ConnectionString);
        await connection.OpenAsync();

        await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {DatabaseName} WITH (FORCE)", connection))
            await drop.ExecuteNonQueryAsync();

        await using (var create = new NpgsqlCommand($"CREATE DATABASE {DatabaseName}", connection))
            await create.ExecuteNonQueryAsync();

        // Первое обращение к Services запускает приложение, а с ним и миграции всех модулей.
        _ = Services;
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();

        if (Directory.Exists(_contentRoot))
            Directory.Delete(_contentRoot, recursive: true);

        return Task.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_contentRoot);

        // Логотипы из тестов пишутся во временный каталог, а не в wwwroot проекта.
        builder.UseContentRoot(_contentRoot);
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.UseSetting("ConnectionStrings:Postgres", _connectionString);
        builder.UseSetting("Jwt:Key", "integration-tests-signing-key-0123456789");
        // Все клиенты TestServer — один адрес, общий предел проверяется отдельным тестом.
        builder.UseSetting("Auth:LoginIpLimitPerMinute", "100000");
    }

    public async Task<TestOrganizer> CreateOrganizerAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var login = $"org-{suffix}";

        using (var scope = Services.CreateScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<AccountService>()
                .CreateTenantWithOwnerAsync($"Org {suffix}", $"org-{suffix}", login, "Organizer", Password);
        }

        return new TestOrganizer(this, await LoginAsync(login));
    }

    public async Task<HttpClient> LoginAsync(string login)
    {
        var response = await CreateClient().PostAsJsonAsync("/auth/login", new { login, password = Password });
        response.EnsureSuccessStatusCode();

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await response.ReadStringAsync("accessToken"));

        return client;
    }

    private static string BuildConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("STATHUB_TEST_POSTGRES")
            ?? new ConfigurationBuilder()
                .AddUserSecrets<Program>(optional: true)
                .Build()
                .GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Задайте STATHUB_TEST_POSTGRES или ConnectionStrings:Postgres в user secrets WebApi.");

        return new NpgsqlConnectionStringBuilder(configured) { Database = DatabaseName }.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<StathubApiFactory>
{
    public const string Name = "api";
}

public static class HttpResponseExtensions
{
    public static async Task<string> ReadStringAsync(this HttpResponseMessage response, string property)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty(property).GetString()!;
    }

    public static async Task<Guid> ReadIdAsync(this HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return Guid.Parse(await response.ReadStringAsync("id"));
    }
}

/// <summary>Организатор со своей организацией и хелперы для подготовки данных через API.</summary>
public sealed class TestOrganizer(StathubApiFactory factory, HttpClient client)
{
    private Guid _leagueId;

    public HttpClient Client { get; } = client;

    public async Task<Guid> CreateTournamentAsync()
    {
        var slug = $"league-{Guid.NewGuid():N}"[..20];

        _leagueId = await (await Client.PostAsJsonAsync("/leagues", new
        {
            name = "Test League",
            slug,
            sport = "Football",
            city = "Perm",
            dataSource = "Native"
        })).ReadIdAsync();

        return await (await Client.PostAsJsonAsync($"/leagues/{_leagueId}/tournaments", new
        {
            name = "Cup",
            startDate = "2026-05-01"
        })).ReadIdAsync();
    }

    public async Task<Guid> CreateStageAsync(Guid tournamentId) =>
        await (await Client.PostAsJsonAsync($"/leagues/{_leagueId}/tournaments/{tournamentId}/stages", new
        {
            name = "Group",
            formatType = "Round",
            winPoints = 3,
            drawPoints = 1,
            lossPoints = 0,
            periodDurationMinutes = 45,
            periodsCount = 2,
            extraTimeEnabled = false,
            penaltyShootoutEnabled = false
        })).ReadIdAsync();

    public async Task<Guid> CreateTeamAsync() =>
        await (await Client.PostAsJsonAsync("/teams", new { name = $"FC {Guid.NewGuid():N}"[..12], city = "Perm" }))
            .ReadIdAsync();

    public async Task<Guid> RegisterTeamAsync(Guid stageId, Guid teamId) =>
        await (await Client.PostAsJsonAsync($"/stages/{stageId}/teams", new { teamId })).ReadIdAsync();

    public async Task<Guid> CreatePlayerAsync() =>
        await (await Client.PostAsJsonAsync("/players", new { fullName = "Ivanov Ivan" })).ReadIdAsync();

    public async Task<Guid> CreateMatchAsync(Guid stageId, Guid homeTeamId, Guid awayTeamId) =>
        await (await Client.PostAsJsonAsync("/matches", new
        {
            stageId,
            homeTeamId,
            awayTeamId,
            startTimeUtc = "2026-05-10T12:00:00Z"
        })).ReadIdAsync();

    /// <summary>Аккаунт с ролью в организации; при указанном матче — назначение на него.</summary>
    public async Task<HttpClient> CreateStaffAsync(string role, Guid? matchId = null)
    {
        var login = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}"[..20];

        var userId = await (await Client.PostAsJsonAsync("/accounts/users", new
        {
            login,
            displayName = role,
            password = StathubApiFactory.Password,
            role
        })).ReadIdAsync();

        if (matchId is { } scopeId)
        {
            var assigned = await Client.PostAsJsonAsync("/accounts/assignments", new
            {
                userId,
                role,
                scopeType = "Match",
                scopeId
            });

            assigned.EnsureSuccessStatusCode();
        }

        return await factory.LoginAsync(login);
    }
}
