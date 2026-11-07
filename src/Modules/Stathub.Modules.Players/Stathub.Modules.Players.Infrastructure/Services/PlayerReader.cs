using Stathub.Modules.Players.Application.Dtos;
using Stathub.Modules.Players.Application.Interfaces;

namespace Stathub.Modules.Players.Infrastructure.Services;

internal sealed class PlayerReader(IPlayerRepository repository) : IPlayerReader
{
    public async Task<bool> ExistsAsync(Guid playerId, CancellationToken cancellationToken = default) =>
        await repository.GetByIdAsync(playerId, cancellationToken) is not null;

    public async Task<IReadOnlyList<PlayerNameDto>> GetNamesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        var players = await repository.GetByIdsAsync(ids, cancellationToken);

        return players
            .Select(player => new PlayerNameDto(player.Id, player.FullName))
            .ToList();
    }
}
