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

    public async Task<IReadOnlyList<DivisionSourceInfo>> GetSourceInfosAsync(
        IReadOnlyCollection<Guid> divisionIds,
        CancellationToken cancellationToken = default)
    {
        // Заявок у одной команды единицы — пакетные читатели в Leagues ради этого не заводим.
        var result = new List<DivisionSourceInfo>();
        foreach (var divisionId in divisionIds.Distinct())
        {
            var division = await divisionReader.GetAsync(divisionId, cancellationToken);
            if (division is null)
                continue;

            var tournament = await tournamentReader.GetPublicInfoAsync(division.TournamentId, cancellationToken);
            if (tournament is null)
                continue;

            var organizerIds = await tournamentReader.GetOrganizerIdsAsync([division.TournamentId], cancellationToken);
            if (organizerIds.Count == 0)
                continue;

            result.Add(new DivisionSourceInfo(
                division.DivisionId,
                division.Name,
                tournament.Id,
                tournament.Name,
                tournament.LeagueName,
                organizerIds[0],
                tournament.IsPublished));
        }

        return result;
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
