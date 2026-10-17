using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stathub.Modules.Rosters.Application;
using Stathub.Modules.Rosters.Application.Interfaces;
using Stathub.Modules.Rosters.Infrastructure.Persistence;
using Stathub.Modules.Rosters.Infrastructure.Services;

namespace Stathub.Modules.Rosters.Infrastructure;

public static class RostersModule
{
    public static IServiceCollection AddRostersModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Строка подключения 'Postgres' не настроена");

        services.AddDbContext<RostersDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ITournamentTeamRepository, TournamentTeamRepository>();
        // Порты заявок реализуются адаптерами поверх читателей соседних модулей.
        services.AddScoped<ITournamentInfoProvider, TournamentInfoProvider>();
        services.AddScoped<ITeamExistsProvider, TeamExistsProvider>();
        services.AddScoped<IPlayerExistsProvider, PlayerExistsProvider>();
        services.AddScoped<RosterService>();

        return services;
    }
}
