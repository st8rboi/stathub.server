using Stathub.Modules.Rosters.Domain.Entities;
using Stathub.Modules.Rosters.Domain.Enums;

namespace Stathub.Modules.Rosters.Application.Dtos;

/// <summary>Заявка команды вместе с составом (включая закрытые заявки — история не скрывается).</summary>
public sealed record TournamentTeamDetailsDto(
    Guid Id,
    Guid DivisionId,
    Guid TournamentId,
    Guid TeamId,
    TournamentTeamStatus Status,
    IReadOnlyList<PlayerRegistrationDto> Registrations,
    DateTime CreatedAtUtc)
{
    internal static TournamentTeamDetailsDto From(TournamentTeam tournamentTeam) => new(
        tournamentTeam.Id,
        tournamentTeam.DivisionId,
        tournamentTeam.TournamentId,
        tournamentTeam.TeamId,
        tournamentTeam.Status,
        tournamentTeam.Registrations
            .OrderBy(registration => registration.JoinedOn)
            .Select(PlayerRegistrationDto.From)
            .ToList(),
        tournamentTeam.CreatedAtUtc);
}
