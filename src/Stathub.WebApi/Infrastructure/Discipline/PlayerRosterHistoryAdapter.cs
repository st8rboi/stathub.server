using Stathub.Modules.Discipline.Application;
using Stathub.Modules.Rosters.Application;

namespace Stathub.WebApi.Infrastructure.Discipline;

/// <summary>
/// История заявок игрока в турнире для дисциплины и допуска к матчу. Адаптер над модулем заявок:
/// Discipline и Matches о составах не знают.
/// </summary>
internal sealed class PlayerRosterHistoryAdapter(RosterService rosterService) : IPlayerRosterHistory
{
    public async Task<IReadOnlyList<PlayerTeamStint>> GetStintsAsync(
        Guid tournamentId,
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var entries = await rosterService.ListByPlayerAsync(playerId, cancellationToken);

        return entries
            .Where(entry => entry.TournamentId == tournamentId)
            .SelectMany(entry => entry.Registrations
                .Where(registration => registration.PlayerId == playerId)
                .Select(registration => new PlayerTeamStint(entry.TeamId, registration.JoinedOn, registration.LeftOn)))
            .ToList();
    }
}
