using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Leagues.Domain.Entities;

namespace Stathub.Modules.Leagues.Infrastructure.Persistence;

public sealed class LeaguesDbContext(DbContextOptions<LeaguesDbContext> options) : DbContext(options)
{
    public DbSet<League> Leagues => Set<League>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("leagues");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LeaguesDbContext).Assembly);
    }
}
