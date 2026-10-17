using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stathub.Modules.Teams.Application;
using Stathub.Modules.Teams.Application.Interfaces;
using Stathub.Modules.Teams.Infrastructure.Persistence;
using Stathub.Modules.Teams.Infrastructure.Services;

namespace Stathub.Modules.Teams.Infrastructure;

public static class TeamsModule
{
    public static IServiceCollection AddTeamsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Строка подключения 'Postgres' не настроена");

        services.AddDbContext<TeamsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ITeamReader, TeamReader>();
        services.AddScoped<TeamService>();

        return services;
    }
}
