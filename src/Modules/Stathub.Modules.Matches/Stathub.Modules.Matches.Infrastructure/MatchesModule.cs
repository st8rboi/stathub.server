using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stathub.Modules.Matches.Application.Interfaces;
using Stathub.Modules.Matches.Infrastructure.Persistence;
using Stathub.Modules.Matches.Infrastructure.Services;
using Stathub.Modules.Leagues.Application.Interfaces;

namespace Stathub.Modules.Matches.Infrastructure;

public static class MatchesModule
{
    public static IServiceCollection AddMatchesModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Строка подключения 'Postgres' не настроена");

        services.AddDbContext<MatchesDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<IMatchFormatProvider, MatchFormatProvider>();
        return services;
    }
}
