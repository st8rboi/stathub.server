using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stathub.Modules.Rosters.Domain.Entities;

namespace Stathub.Modules.Rosters.Infrastructure.Persistence.Configurations;

internal sealed class PlayerRegistrationConfiguration : IEntityTypeConfiguration<PlayerRegistration>
{
    public void Configure(EntityTypeBuilder<PlayerRegistration> builder)
    {
        builder.ToTable("player_registrations");

        builder.HasKey(registration => registration.Id);
        builder.Property(registration => registration.Id).ValueGeneratedNever();

        builder.Property(registration => registration.TournamentTeamId).IsRequired();
        builder.Property(registration => registration.PlayerId).IsRequired();
        builder.Property(registration => registration.SquadNumber);
        builder.Property(registration => registration.Status).IsRequired();
        builder.Property(registration => registration.JoinedOn).IsRequired();
        builder.Property(registration => registration.LeftOn);

        builder.HasIndex(registration => registration.PlayerId);
        builder.HasIndex(registration => registration.TournamentTeamId);
    }
}
