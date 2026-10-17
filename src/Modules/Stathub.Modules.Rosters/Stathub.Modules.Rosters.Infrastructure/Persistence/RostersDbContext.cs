using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Rosters.Domain.Entities;

namespace Stathub.Modules.Rosters.Infrastructure.Persistence;

public sealed class RostersDbContext(DbContextOptions<RostersDbContext> options) : DbContext(options)
{
    public DbSet<TournamentTeam> TournamentTeams => Set<TournamentTeam>();
    public DbSet<PlayerRegistration> PlayerRegistrations => Set<PlayerRegistration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("rosters");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RostersDbContext).Assembly);
    }
}
