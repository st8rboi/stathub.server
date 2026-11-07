using Stathub.Modules.Matches.Application.Dtos;
using Stathub.Modules.Matches.Application.Interfaces;
using Stathub.Modules.Matches.Domain.Enums;

namespace Stathub.Modules.Matches.Infrastructure.Services;

internal sealed class MatchResultsReader(IMatchRepository repository) : IMatchResultsReader
{
    public async Task<IReadOnlyList<FinishedMatchDto>> GetFinishedByStageAsync(
        Guid stageId,
        CancellationToken cancellationToken = default)
    {
        var matches = await repository.GetByStageIdAsync(stageId, cancellationToken);

        return matches
            .Where(match => match.Status == MatchStatus.Finished)
            .Select(match => new FinishedMatchDto(
                match.HomeTeamId,
                match.AwayTeamId,
                match.HomeScore,
                match.AwayScore))
            .ToList();
    }
}
