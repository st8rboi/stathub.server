namespace Stathub.WebApi.Contracts.Rosters;

/// <summary>
/// Трансфер: игрок закрывает заявку в команде-источнике и открывает её в команде назначения
/// того же турнира. Обе записи остаются в истории.
/// </summary>
public sealed record TransferPlayerRequest(
    Guid PlayerId,
    Guid FromTeamId,
    Guid ToTeamId,
    int? SquadNumber);
