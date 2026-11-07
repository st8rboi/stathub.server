using Stathub.Modules.Players.Application.Interfaces;
using Stathub.Modules.Statistics.Application.Interfaces;

namespace Stathub.Modules.Statistics.Infrastructure.Services;

/// <summary>Адаптер «порт статистики → читатель игроков модуля Players».</summary>
public sealed class PlayerNameSource(IPlayerReader playerReader) : IPlayerNameSource
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> playerIds,
        CancellationToken cancellationToken = default)
    {
        if (playerIds.Count == 0)
            return new Dictionary<Guid, string>();

        var players = await playerReader.GetNamesAsync(playerIds, cancellationToken);

        return players.ToDictionary(player => player.Id, player => player.FullName);
    }
}
