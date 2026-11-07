using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stathub.Modules.Rosters.Domain.Entities;

namespace Stathub.Modules.Rosters.Infrastructure.Persistence.Configurations;

internal sealed class TournamentTeamConfiguration : IEntityTypeConfiguration<TournamentTeam>
{
    public void Configure(EntityTypeBuilder<TournamentTeam> builder)
    {
        builder.ToTable("tournament_teams");

        builder.HasKey(tournamentTeam => tournamentTeam.Id);
        builder.Property(tournamentTeam => tournamentTeam.Id).ValueGeneratedNever();

        builder.Property(tournamentTeam => tournamentTeam.StageId).IsRequired();
        builder.Property(tournamentTeam => tournamentTeam.TournamentId).IsRequired();
        builder.Property(tournamentTeam => tournamentTeam.TeamId).IsRequired();
        builder.Property(tournamentTeam => tournamentTeam.Status).IsRequired();
        builder.Property(tournamentTeam => tournamentTeam.CreatedAtUtc).IsRequired();

        // Команда заявляется один раз на этап. В разных дивизионах того же турнира
        // одна и та же команда может играть — это отдельные заявки.
        builder.HasIndex(tournamentTeam => new { tournamentTeam.StageId, tournamentTeam.TeamId }).IsUnique();
        builder.HasIndex(tournamentTeam => tournamentTeam.TournamentId);

        builder
            .HasMany(tournamentTeam => tournamentTeam.Registrations)
            .WithOne()
            .HasForeignKey(registration => registration.TournamentTeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(tournamentTeam => tournamentTeam.Registrations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
