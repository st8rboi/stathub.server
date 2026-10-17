using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stathub.Modules.Players.Application;
using Stathub.Modules.Players.Application.Interfaces;
using Stathub.Modules.Players.Infrastructure.Persistence;

namespace Stathub.Modules.Players.Infrastructure;

public static class PlayersModule
{
    public static IServiceCollection AddPlayersModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Строка подключения 'Postgres' не настроена");

        services.AddDbContext<PlayersDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<PlayerService>();

        return services;
    }
}
