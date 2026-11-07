using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stathub.Modules.Identity.Application;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Modules.Identity.Infrastructure.Persistence;
using Stathub.Modules.Identity.Infrastructure.Services;

namespace Stathub.Modules.Identity.Infrastructure;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Строка подключения 'Postgres' не настроена");

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(
                    "__EFMigrationsHistory_Identity",
                    "public")));

        // Читаем настройки вручную: класс-опция простой, а Binder в зависимостях модуля не нужен.
        var jwtOptions = new JwtOptions
        {
            Issuer = configuration["Jwt:Issuer"] ?? "stathub",
            Audience = configuration["Jwt:Audience"] ?? "stathub-api",
            Key = configuration["Jwt:Key"] ?? string.Empty,
            AccessTokenMinutes = int.TryParse(configuration["Jwt:AccessTokenMinutes"], out var minutes) ? minutes : 15,
            RefreshTokenDays = int.TryParse(configuration["Jwt:RefreshTokenDays"], out var days) ? days : 30
        };

        jwtOptions.Validate();
        services.AddSingleton(jwtOptions);

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRoleAssignmentRepository, UserRoleAssignmentRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.AddScoped<AuthService>();
        services.AddScoped<AccountService>();

        return services;
    }
}
