using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stathub.Modules.Matches.Domain.Entities;

namespace Stathub.Modules.Matches.Infrastructure.Persistence.Configurations;

internal sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("matches");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.StageId).IsRequired();
        builder.Property(m => m.HomeTeamId).IsRequired();
        builder.Property(m => m.AwayTeamId).IsRequired();
        builder.Property(m => m.StartedAtUtc).IsRequired();
        builder.Property(m => m.Status).IsRequired();

    }
}
