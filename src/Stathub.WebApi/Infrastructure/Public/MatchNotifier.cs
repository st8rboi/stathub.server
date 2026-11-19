using Microsoft.AspNetCore.SignalR;
using Stathub.WebApi.Hubs;

namespace Stathub.WebApi.Infrastructure.Public;

/// <summary>
/// Уведомления зрителей после коммита (documentation/doc.md §4, §9): пуш — best-effort,
/// источником истины остаётся REST-состояние, поэтому в сообщении только идентификатор.
/// </summary>
public sealed class MatchNotifier(IHubContext<MatchHub> hub)
{
    public async Task PublishMatchChangedAsync(
        Guid matchId,
        Guid stageId,
        CancellationToken cancellationToken = default)
    {
        await hub.Clients.Group(MatchHub.MatchGroup(matchId))
            .SendAsync("MatchUpdated", matchId, cancellationToken);

        // Live-таблица этапа учитывает текущий счёт идущих матчей — меняется после каждого гола.
        await hub.Clients.Group(MatchHub.StageGroup(stageId))
            .SendAsync("StageUpdated", stageId, cancellationToken);

        // Список «сейчас играют» меняется вместе со статусом или счётом любого матча.
        await hub.Clients.Group(MatchHub.LiveGroup)
            .SendAsync("LiveFeedChanged", matchId, cancellationToken);
    }
}
