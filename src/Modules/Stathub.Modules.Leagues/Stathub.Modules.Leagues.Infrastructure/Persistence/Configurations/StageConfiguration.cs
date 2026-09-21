using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stathub.Modules.Leagues.Domain.Entities;

namespace Stathub.Modules.Leagues.Infrastructure.Persistence.Configurations;

internal sealed class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.ToTable("stages");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.TournamentId).IsRequired();
        builder.Property(s => s.Order).IsRequired();
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.FormatType).IsRequired();

        builder.OwnsOne(s => s.PointsRule, rule =>
        {
            rule.Property(r => r.WinPoints).HasColumnName("win_points").IsRequired();
            rule.Property(r => r.DrawPoints).HasColumnName("draw_points").IsRequired();
            rule.Property(r => r.LossPoints).HasColumnName("loss_points").IsRequired();
        });

        builder.OwnsOne(s => s.MatchFormatRule, rule =>
        {
            rule.Property(r => r.PeriodDurationMinutes).HasColumnName("period_duration_minutes").IsRequired();
            rule.Property(r => r.PeriodsCount).HasColumnName("periods_count").IsRequired();
            rule.Property(r => r.ExtraTimeEnabled).HasColumnName("extra_time_enabled").IsRequired();
            rule.Property(r => r.PenaltyShootoutEnabled).HasColumnName("penalty_shootout_enabled").IsRequired();
        });

        builder.Navigation(s => s.PointsRule).IsRequired();
        builder.Navigation(s => s.MatchFormatRule).IsRequired();

        builder.HasMany(s => s.TiebreakerRules)
            .WithOne()
            .HasForeignKey(r => r.StageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.TiebreakerRules).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
