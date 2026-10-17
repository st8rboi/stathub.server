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
        // Турнир этапа — контекст заявок и составов; существующие матчи получают пустой Guid
        // миграцией, новые заполняются из этапа при создании.
        builder.Property(m => m.TournamentId).IsRequired();
        builder.Property(m => m.HomeTeamId).IsRequired();
        builder.Property(m => m.AwayTeamId).IsRequired();
        builder.Property(m => m.CreatedAtUtc).IsRequired();
        builder.Property(m => m.ScheduledTimeUtc).IsRequired();
        // StartedAtUtc заполняется только при старте матча, поэтому в Scheduled он NULL
        builder.Property(m => m.StartedAtUtc);
        builder.Property(m => m.Status).IsRequired();
        builder.Property(m => m.HomeScore).IsRequired();
        builder.Property(m => m.AwayScore).IsRequired();

        // Снимок правил этапа на момент создания матча — обязателен
        builder.Property(m => m.PeriodsCount).IsRequired();
        builder.Property(m => m.PeriodDurationMinutes).IsRequired();
        builder.Property(m => m.ExtraTimeEnabled).IsRequired();
        builder.Property(m => m.PenaltyShootoutEnabled).IsRequired();

        // Заполняются только когда матч начат, до старта CurrentPeriod = 0
        builder.Property(m => m.CurrentPeriod).IsRequired();
        builder.Property(m => m.PeriodStartedAtUtc);

        builder.HasIndex(m => m.StageId);
    }
}
