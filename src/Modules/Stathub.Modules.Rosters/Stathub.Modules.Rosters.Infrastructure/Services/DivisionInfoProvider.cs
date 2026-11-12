using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Rosters.Application.Interfaces;

namespace Stathub.Modules.Rosters.Infrastructure.Services;

/// <summary>
/// Адаптер «порт заявок → читатели модуля Leagues»: дивизион даёт турнир, турнир — блокировку заявок.
/// </summary>
internal sealed class DivisionInfoProvider(
    IDivisionReader divisionReader,
    IStagePublicReader stagePublicReader,
    ITournamentReader tournamentReader) : IDivisionInfoProvider
{
    public async Task<DivisionRosterInfo?> GetAsync(Guid divisionId, CancellationToken cancellationToken = default)
    {
        var division = await divisionReader.GetAsync(divisionId, cancellationToken);
        if (division is null)
            return null;

        return await BuildAsync(division.DivisionId, division.TournamentId, cancellationToken);
    }

    public async Task<DivisionRosterInfo?> GetByStageAsync(Guid stageId, CancellationToken cancellationToken = default)
    {
        var stage = await stagePublicReader.GetAsync(stageId, cancellationToken);
        if (stage is null)
            return null;

        return await BuildAsync(stage.DivisionId, stage.TournamentId, cancellationToken);
    }

    private async Task<DivisionRosterInfo?> BuildAsync(
        Guid divisionId,
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await tournamentReader.GetAsync(tournamentId, cancellationToken);
        if (tournament is null)
            return null;

        return new DivisionRosterInfo(divisionId, tournamentId, tournament.RosterLocked);
    }
}
