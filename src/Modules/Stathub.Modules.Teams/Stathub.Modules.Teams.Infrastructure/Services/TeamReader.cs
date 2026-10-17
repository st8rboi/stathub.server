using Stathub.Modules.Teams.Application.Dtos;
using Stathub.Modules.Teams.Application.Interfaces;

namespace Stathub.Modules.Teams.Infrastructure.Services;

internal sealed class TeamReader(ITeamRepository repository) : ITeamReader
{
    public async Task<bool> ExistsAsync(Guid teamId, CancellationToken cancellationToken = default) =>
        await repository.GetByIdAsync(teamId, cancellationToken) is not null;

    public async Task<IReadOnlyList<TeamNameDto>> GetNamesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var teams = await repository.GetByIdsAsync(ids, cancellationToken);

        return teams.Select(team => new TeamNameDto(team.Id, team.Name, team.LogoUrl)).ToList();
    }
}
