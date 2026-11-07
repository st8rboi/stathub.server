using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stathub.Modules.Rosters.Application;
using Stathub.Modules.Rosters.Application.Interfaces;
using Stathub.Modules.Rosters.Infrastructure.Persistence;
using Stathub.Modules.Rosters.Infrastructure.Services;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Players.Application.Interfaces;
using Stathub.Modules.Teams.Application.Interfaces;

namespace Stathub.Modules.Rosters.Infrastructure;

public static class RostersModule
{
    public static IServiceCollection AddRostersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Строка подключения 'Postgres' не настроена");

        services.AddDbContext<RostersDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(
                    "__EFMigrationsHistory_Rosters",
                    "public")));

        services.AddScoped<ITournamentTeamRepository, TournamentTeamRepository>();
        services.AddScoped<IStageInfoProvider, StageInfoProvider>();
        services.AddScoped<ITeamExistsProvider, TeamExistsProvider>();
        services.AddScoped<IPlayerExistsProvider, PlayerExistsProvider>();
        services.AddScoped<ITeamUsageSource, RosterTeamUsageSource>();
        services.AddScoped<IPlayerUsageSource, RosterPlayerUsageSource>();
        services.AddScoped<ICompetitionUsageSource, RosterCompetitionUsageSource>();
        services.AddScoped<RosterService>();

        return services;
    }
}
