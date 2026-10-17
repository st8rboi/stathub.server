using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stathub.Modules.Teams.Domain.Entities;

namespace Stathub.Modules.Teams.Infrastructure.Persistence.Configurations;

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams");

        builder.HasKey(team => team.Id);
        builder.Property(team => team.Id).ValueGeneratedNever();

        builder.Property(team => team.Name).IsRequired().HasMaxLength(150);
        builder.Property(team => team.City).HasMaxLength(100);
        builder.Property(team => team.LogoUrl).HasMaxLength(500);
        builder.Property(team => team.CreatedAtUtc).IsRequired();

        builder.HasIndex(team => team.Name);
    }
}
