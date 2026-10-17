using Stathub.Modules.Statistics.Application.Dtos;
using Stathub.Modules.Statistics.Application.Interfaces;
using Stathub.Modules.Teams.Application.Interfaces;

namespace Stathub.Modules.Statistics.Infrastructure.Services;

/// <summary>Адаптер «порт статистики → читатель команд модуля Teams».</summary>
public sealed class TeamBadgeSource(ITeamReader teamReader) : ITeamBadgeSource
{
    public async Task<IReadOnlyDictionary<Guid, TeamBadgeDto>> GetAsync(
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken = default)
    {
        if (teamIds.Count == 0)
            return new Dictionary<Guid, TeamBadgeDto>();

        var teams = await teamReader.GetNamesAsync(teamIds, cancellationToken);

        return teams.ToDictionary(
            team => team.Id,
            team => new TeamBadgeDto(team.Id, team.Name, team.LogoUrl));
    }
}
