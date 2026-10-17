using Stathub.Modules.Matches.Application.Interfaces;
using Stathub.Modules.Standings.Application.Interfaces;
using Stathub.Modules.Standings.Domain.Models;

namespace Stathub.Modules.Standings.Infrastructure.Services;

/// <summary>Адаптер «порт таблицы → читатель результатов модуля Matches».</summary>
public sealed class MatchResultsSource(IMatchResultsReader matchResultsReader) : IStandingsMatchSource
{
    public async Task<IReadOnlyList<StandingsMatch>> GetFinishedMatchesAsync(
        Guid stageId,
        CancellationToken cancellationToken = default)
    {
        var matches = await matchResultsReader.GetFinishedByStageAsync(stageId, cancellationToken);

        return matches
            .Select(match => new StandingsMatch(
                match.HomeTeamId,
                match.AwayTeamId,
                match.HomeScore,
                match.AwayScore))
            .ToList();
    }
}
