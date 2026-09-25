using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stathub.Modules.Leagues.Application;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Leagues.Infrastructure.Persistence;

namespace Stathub.Modules.Leagues.Infrastructure;

public static class LeaguesModule
{
    public static IServiceCollection AddLeaguesModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Строка подключения 'Postgres' не настроена");

        services.AddDbContext<LeaguesDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ILeagueRepository, LeagueRepository>();
        services.AddScoped<ITournamentRepository, TournamentRepository>();
        services.AddScoped<LeagueService>();
        services.AddScoped<TournamentService>();

        return services;
    }
}
