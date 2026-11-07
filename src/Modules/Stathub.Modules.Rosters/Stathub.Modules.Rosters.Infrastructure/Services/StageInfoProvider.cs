using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Rosters.Application.Interfaces;

namespace Stathub.Modules.Rosters.Infrastructure.Services;

/// <summary>
/// Адаптер «порт заявок → читатели модуля Leagues»: этап даёт турнир, турнир — блокировку заявок.
/// </summary>
internal sealed class StageInfoProvider(
    IStagePublicReader stagePublicReader,
    ITournamentReader tournamentReader) : IStageInfoProvider
{
    public async Task<StageRosterInfo?> GetAsync(Guid stageId, CancellationToken cancellationToken = default)
    {
        var stage = await stagePublicReader.GetAsync(stageId, cancellationToken);
        if (stage is null)
            return null;

        var tournament = await tournamentReader.GetAsync(stage.TournamentId, cancellationToken);
        if (tournament is null)
            return null;

        return new StageRosterInfo(stage.StageId, stage.TournamentId, tournament.RosterLocked);
    }
}
