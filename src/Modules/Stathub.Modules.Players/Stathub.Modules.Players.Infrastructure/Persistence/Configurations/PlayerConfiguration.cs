using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stathub.Modules.Players.Domain.Entities;

namespace Stathub.Modules.Players.Infrastructure.Persistence.Configurations;

internal sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("players");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.FullName).IsRequired().HasMaxLength(200);

        // Дата рождения необязательна: организатор может её не знать
        builder.Property(p => p.BirthDate);

        builder.Property(p => p.CreatedAtUtc).IsRequired();

        // Подсказки ищутся через similarity/ILIKE — без этого индекса поиск превратится
        // в последовательное сканирование таблицы игроков.
        builder.HasIndex(p => p.FullName)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
    }
}
