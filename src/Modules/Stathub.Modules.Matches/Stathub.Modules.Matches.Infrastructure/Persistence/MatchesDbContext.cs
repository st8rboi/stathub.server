using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Matches.Domain.Entities;

namespace Stathub.Modules.Matches.Infrastructure.Persistence;

public sealed class MatchesDbContext(DbContextOptions<MatchesDbContext> options) : DbContext(options)
{
    public DbSet<Match> Matches => Set<Match>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("matches");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MatchesDbContext).Assembly);
    }
}
