using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stathub.Modules.Leagues.Domain.Entities;

namespace Stathub.Modules.Leagues.Infrastructure.Persistence.Configurations;

internal sealed class TournamentConfiguration : IEntityTypeConfiguration<Tournament>
{
    public void Configure(EntityTypeBuilder<Tournament> builder)
    {
        builder.ToTable("tournaments");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.LeagueId).IsRequired();
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.StartDate).IsRequired();
        builder.Property(t => t.EndDate);
        builder.Property(t => t.Status).IsRequired();
        builder.Property(t => t.CreatedAtUtc).IsRequired();

        builder.HasIndex(t => t.LeagueId);

        builder.HasMany(t => t.Stages)
            .WithOne()
            .HasForeignKey(s => s.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(t => t.Stages).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
