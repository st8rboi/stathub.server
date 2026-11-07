using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Teams.Domain.Entities;

namespace Stathub.Modules.Teams.Infrastructure.Persistence;

public sealed class TeamsDbContext(DbContextOptions<TeamsDbContext> options) : DbContext(options)
{
    public DbSet<Team> Teams => Set<Team>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("teams");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TeamsDbContext).Assembly);
    }
}
