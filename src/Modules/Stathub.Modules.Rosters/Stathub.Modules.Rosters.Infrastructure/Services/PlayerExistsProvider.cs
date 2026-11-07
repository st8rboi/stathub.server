using Stathub.Modules.Players.Application.Interfaces;
using Stathub.Modules.Rosters.Application.Interfaces;

namespace Stathub.Modules.Rosters.Infrastructure.Services;

internal sealed class PlayerExistsProvider(IPlayerReader playerReader) : IPlayerExistsProvider
{
    public Task<bool> ExistsAsync(Guid playerId, CancellationToken cancellationToken = default) =>
        playerReader.ExistsAsync(playerId, cancellationToken);
}
