using Microsoft.AspNetCore.SignalR;

namespace Stathub.WebApi.Hubs;

/// <summary>
/// Однонаправленный хаб для зрителей (documentation/doc.md §4): судья не шлёт события сюда,
/// сервер сам рассылает изменения после коммита. Группа матча — <c>match:{id}</c>,
/// группа этапа (live-таблица) — <c>stage:{id}</c>, лента живых матчей — <c>live</c>.
/// </summary>
public sealed class MatchHub : Hub
{
    public const string LiveGroup = "live";

    public static string MatchGroup(Guid matchId) => $"match:{matchId}";

    public static string StageGroup(Guid stageId) => $"stage:{stageId}";

    public Task SubscribeMatch(Guid matchId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, MatchGroup(matchId));

    public Task UnsubscribeMatch(Guid matchId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, MatchGroup(matchId));

    public Task SubscribeStage(Guid stageId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, StageGroup(stageId));

    public Task UnsubscribeStage(Guid stageId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, StageGroup(stageId));

    public Task SubscribeLive() => Groups.AddToGroupAsync(Context.ConnectionId, LiveGroup);

    public Task UnsubscribeLive() => Groups.RemoveFromGroupAsync(Context.ConnectionId, LiveGroup);
}
