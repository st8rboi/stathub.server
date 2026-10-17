using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stathub.Modules.Matches.Domain.Entities;

namespace Stathub.Modules.Matches.Infrastructure.Persistence.Configurations;

internal sealed class MatchEventConfiguration : IEntityTypeConfiguration<MatchEvent>
{
    public void Configure(EntityTypeBuilder<MatchEvent> builder)
    {
        builder.ToTable("match_events");

        builder.HasKey(matchEvent => matchEvent.Id);
        builder.Property(matchEvent => matchEvent.Id).ValueGeneratedNever();

        builder.Property(matchEvent => matchEvent.MatchId).IsRequired();
        builder.Property(matchEvent => matchEvent.Type).IsRequired();

        // Игроки пока приходят GUID-ами из модуля Players без FK:
        // межмодульные связи запрещены, а PlayerRegistration ещё не существует.
        builder.Property(matchEvent => matchEvent.TeamId);
        builder.Property(matchEvent => matchEvent.PlayerId);
        builder.Property(matchEvent => matchEvent.RelatedPlayerId);
        builder.Property(matchEvent => matchEvent.TargetEventId);

        builder.Property(matchEvent => matchEvent.Reason).HasMaxLength(512);

        builder.Property(matchEvent => matchEvent.ClientTimestampUtc).IsRequired();
        builder.Property(matchEvent => matchEvent.SequenceNumber).IsRequired();
        builder.Property(matchEvent => matchEvent.CreatedAtUtc).IsRequired();

        // FK внутри модуля: событие не может существовать без своего матча.
        builder.HasOne<Match>()
            .WithMany()
            .HasForeignKey(matchEvent => matchEvent.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // Протокол всегда читается в порядке клиентской нумерации.
        builder.HasIndex(matchEvent => new { matchEvent.MatchId, matchEvent.SequenceNumber });
    }
}
