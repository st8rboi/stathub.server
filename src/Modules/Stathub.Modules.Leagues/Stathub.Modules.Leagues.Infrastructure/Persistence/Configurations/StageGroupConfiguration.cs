using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stathub.Modules.Leagues.Domain.Entities;

namespace Stathub.Modules.Leagues.Infrastructure.Persistence.Configurations;

internal sealed class StageGroupConfiguration : IEntityTypeConfiguration<StageGroup>
{
    public void Configure(EntityTypeBuilder<StageGroup> builder)
    {
        builder.ToTable("stage_groups");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.StageId).IsRequired();
        builder.Property(g => g.Order).IsRequired();
        builder.Property(g => g.Name).IsRequired().HasMaxLength(200);

        // Команды группы — массив идентификаторов (uuid[]): команды живут в модуле Teams,
        // FK между модулями не делаем, а отдельная таблица связей ради списка Id не нужна.
        builder.Ignore(g => g.TeamIds);
        builder.Property<List<Guid>>("_teamIds")
            .HasColumnName("TeamIds")
            .IsRequired();
    }
}
