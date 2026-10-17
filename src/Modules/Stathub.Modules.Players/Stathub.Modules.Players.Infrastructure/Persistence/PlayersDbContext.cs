using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Players.Domain.Entities;

namespace Stathub.Modules.Players.Infrastructure.Persistence;

public sealed class PlayersDbContext(DbContextOptions<PlayersDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("players");

        // Триграммы нужны для устойчивого к опечаткам поиска игроков (documentation/doc.md §9.3).
        // ВАЖНО: CREATE EXTENSION выполняется от имени владельца БД и требует прав
        // на уровне сервера — на VPS это может быть разовое действие администратора БД.
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlayersDbContext).Assembly);
    }
}
