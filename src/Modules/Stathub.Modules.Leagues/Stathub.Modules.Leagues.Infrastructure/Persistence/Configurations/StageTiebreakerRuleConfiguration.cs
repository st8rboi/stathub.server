using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stathub.Modules.Leagues.Domain.Entities;

namespace Stathub.Modules.Leagues.Infrastructure.Persistence.Configurations;

internal sealed class StageTiebreakerRuleConfiguration : IEntityTypeConfiguration<StageTiebreakerRule>
{
    public void Configure(EntityTypeBuilder<StageTiebreakerRule> builder)
    {
        builder.ToTable("stage_tiebreaker_rules");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.StageId).IsRequired();
        builder.Property(r => r.Priority).IsRequired();
        builder.Property(r => r.Criterion).IsRequired();
    }
}
