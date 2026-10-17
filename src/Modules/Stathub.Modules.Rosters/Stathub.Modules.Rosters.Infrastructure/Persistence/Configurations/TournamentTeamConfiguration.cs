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

        // Ссылки на турнир и команду — обычные Guid: межмодульные FK запрещены,
        // существование проверяется читателями модулей Leagues и Teams.
        builder.Property(tournamentTeam => tournamentTeam.TournamentId).IsRequired();
        builder.Property(tournamentTeam => tournamentTeam.TeamId).IsRequired();
        builder.Property(tournamentTeam => tournamentTeam.Status).IsRequired();
        builder.Property(tournamentTeam => tournamentTeam.CreatedAtUtc).IsRequired();

        // Одна команда — одна заявка на турнир.
        builder.HasIndex(tournamentTeam => new { tournamentTeam.TournamentId, tournamentTeam.TeamId })
            .IsUnique();

        builder.HasMany(tournamentTeam => tournamentTeam.Registrations)
            .WithOne()
            .HasForeignKey(registration => registration.TournamentTeamId)
            .OnDelete(DeleteBehavior.Cascade);

        // Состав живёт в приватном списке _registrations.
        builder.Navigation(tournamentTeam => tournamentTeam.Registrations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
