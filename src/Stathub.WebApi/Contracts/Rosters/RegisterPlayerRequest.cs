namespace Stathub.WebApi.Contracts.Rosters;

/// <summary>
/// Заявка игрока за команду турнира. Игровой номер необязателен и уникален среди активных
/// заявок команды.
/// </summary>
public sealed record RegisterPlayerRequest(Guid PlayerId, int? SquadNumber);
