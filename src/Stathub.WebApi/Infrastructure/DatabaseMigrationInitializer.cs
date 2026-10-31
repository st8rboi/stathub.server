using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stathub.Modules.Finance.Infrastructure.Persistence;
using Stathub.Modules.Identity.Infrastructure.Persistence;
using Stathub.Modules.Leagues.Infrastructure.Persistence;
using Stathub.Modules.Matches.Infrastructure.Persistence;
using Stathub.Modules.Players.Infrastructure.Persistence;
using Stathub.Modules.Rosters.Infrastructure.Persistence;
using Stathub.Modules.Teams.Infrastructure.Persistence;

namespace Stathub.WebApi.Infrastructure;

public static class DatabaseMigrationInitializer
{
    public static async Task ApplyMigrationsAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await ApplyAsync<IdentityDbContext>(
            serviceProvider, cancellationToken);

        await ApplyAsync<TeamsDbContext>(
            serviceProvider, cancellationToken);

        await ApplyAsync<PlayersDbContext>(
            serviceProvider, cancellationToken);

        await ApplyAsync<RostersDbContext>(
            serviceProvider, cancellationToken);

        await ApplyAsync<LeaguesDbContext>(
            serviceProvider, cancellationToken);

        await ApplyAsync<MatchesDbContext>(
            serviceProvider, cancellationToken);

        await ApplyAsync<FinanceDbContext>(
            serviceProvider, cancellationToken);
    }

    private static async Task ApplyAsync<TContext>(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<TContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}