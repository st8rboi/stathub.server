using Microsoft.AspNetCore.SignalR;
using Stathub.WebApi.Hubs;

namespace Stathub.WebApi.Infrastructure.Public;

/// <summary>
/// Уведомления зрителей после коммита (documentation/doc.md §4, §9): пуш — best-effort,
/// источником истины остаётся REST-состояние, поэтому в сообщении только идентификатор матча.
/// </summary>
public sealed class MatchNotifier(IHubContext<MatchHub> hub)
{
    public async Task PublishMatchChangedAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        await hub.Clients.Group(MatchHub.MatchGroup(matchId))
            .SendAsync("MatchUpdated", matchId, cancellationToken);

        // Список «сейчас играют» меняется вместе со статусом или счётом любого матча.
        await hub.Clients.Group(MatchHub.LiveGroup)
            .SendAsync("LiveFeedChanged", matchId, cancellationToken);
    }
}
