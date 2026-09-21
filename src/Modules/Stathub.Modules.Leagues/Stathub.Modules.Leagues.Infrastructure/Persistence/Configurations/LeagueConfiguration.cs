using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stathub.Modules.Leagues.Domain.Entities;

namespace Stathub.Modules.Leagues.Infrastructure.Persistence.Configurations;

internal sealed class LeagueConfiguration : IEntityTypeConfiguration<League>
{
    public void Configure(EntityTypeBuilder<League> builder)
    {
        builder.ToTable("leagues");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.OrganizerId).IsRequired();
        builder.Property(l => l.Name).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Slug).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Sport).IsRequired();
        builder.Property(l => l.City).HasMaxLength(200);
        builder.Property(l => l.Region).HasMaxLength(200);
        builder.Property(l => l.DataSource).IsRequired();
        builder.Property(l => l.Status).IsRequired();
        builder.Property(l => l.CreatedAtUtc).IsRequired();

        builder.HasIndex(l => l.Slug).IsUnique();
        builder.HasIndex(l => l.City);
    }
}
