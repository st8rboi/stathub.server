using Stathub.Modules.Rosters.Domain.Entities;
using Stathub.Modules.Rosters.Domain.Enums;

namespace Stathub.Modules.Rosters.Application.Dtos;

/// <summary>Заявка команды в списке участников турнира.</summary>
public sealed record TournamentTeamDto(
    Guid Id,
    Guid TournamentId,
    Guid TeamId,
    TournamentTeamStatus Status,
    int ActivePlayers,
    DateTime CreatedAtUtc)
{
    internal static TournamentTeamDto From(TournamentTeam tournamentTeam) => new(
        tournamentTeam.Id,
        tournamentTeam.TournamentId,
        tournamentTeam.TeamId,
        tournamentTeam.Status,
        tournamentTeam.Registrations.Count(registration =>
            registration.Status == PlayerRegistrationStatus.Active),
        tournamentTeam.CreatedAtUtc);
}
