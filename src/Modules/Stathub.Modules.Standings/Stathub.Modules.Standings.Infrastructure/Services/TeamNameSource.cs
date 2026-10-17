using Stathub.Modules.Standings.Application.Dtos;
using Stathub.Modules.Standings.Application.Interfaces;
using Stathub.Modules.Teams.Application.Interfaces;

namespace Stathub.Modules.Standings.Infrastructure.Services;

/// <summary>Адаптер «порт таблицы → читатель команд модуля Teams».</summary>
public sealed class TeamNameSource(ITeamReader teamReader) : IStandingsTeamNameSource
{
    public async Task<IReadOnlyDictionary<Guid, StandingsTeamDto>> GetTeamsAsync(
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken = default)
    {
        if (teamIds.Count == 0)
            return new Dictionary<Guid, StandingsTeamDto>();

        var teams = await teamReader.GetNamesAsync(teamIds, cancellationToken);

        return teams.ToDictionary(
            team => team.Id,
            team => new StandingsTeamDto(team.Id, team.Name, team.LogoUrl));
    }
}
