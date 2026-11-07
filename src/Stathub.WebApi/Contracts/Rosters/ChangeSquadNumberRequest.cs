namespace Stathub.WebApi.Contracts.Rosters;

/// <summary>
/// Смена игрового номера в активной заявке. null — без номера. Номер уникален среди активных
/// заявок команды.
/// </summary>
public sealed record ChangeSquadNumberRequest(int? SquadNumber);
