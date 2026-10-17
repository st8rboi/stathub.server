using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Rosters.Application.Interfaces;

namespace Stathub.Modules.Rosters.Infrastructure.Services;

/// <summary>
/// Адаптер «порт заявок → читатель модуля Leagues». Единственное место, где Rosters знает
/// о контрактах соседнего модуля: дальше по слоям ссылка не протекает.
/// </summary>
internal sealed class TournamentInfoProvider(ITournamentReader tournamentReader) : ITournamentInfoProvider
{
    public async Task<TournamentRosterInfo?> GetAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        var tournament = await tournamentReader.GetAsync(tournamentId, cancellationToken);

        return tournament is null
            ? null
            : new TournamentRosterInfo(tournament.TournamentId, tournament.RosterLocked);
    }
}
