using Stathub.Modules.Matches.Application.Dtos;
using Stathub.Modules.Matches.Application.Interfaces;
using Stathub.Modules.Matches.Domain.Enums;

namespace Stathub.Modules.Matches.Infrastructure.Services;

internal sealed class MatchResultsReader(IMatchRepository repository) : IMatchResultsReader
{
    public async Task<IReadOnlyList<StageMatchResultDto>> GetResultsByStageAsync(
        Guid stageId,
        CancellationToken cancellationToken = default)
    {
        var matches = await repository.GetByStageIdAsync(stageId, cancellationToken);

        return matches
            .Where(match => match.Status is MatchStatus.Finished or MatchStatus.InProgress or MatchStatus.Pause)
            .Select(match => new StageMatchResultDto(
                match.Id,
                match.HomeTeamId,
                match.AwayTeamId,
                match.HomeScore,
                match.AwayScore,
                match.GroupId,
                IsLive: match.Status != MatchStatus.Finished))
            .ToList();
    }
}
