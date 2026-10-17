using Stathub.Modules.Rosters.Application.Interfaces;
using Stathub.Modules.Teams.Application.Interfaces;

namespace Stathub.Modules.Rosters.Infrastructure.Services;

internal sealed class TeamExistsProvider(ITeamReader teamReader) : ITeamExistsProvider
{
    public Task<bool> ExistsAsync(Guid teamId, CancellationToken cancellationToken = default) =>
        teamReader.ExistsAsync(teamId, cancellationToken);
}
