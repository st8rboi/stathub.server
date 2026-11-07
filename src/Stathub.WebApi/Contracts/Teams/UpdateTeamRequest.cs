namespace Stathub.WebApi.Contracts.Teams;

/// <summary>
/// Изменение названия и города команды. Город необязателен (null или пусто — без города).
/// </summary>
public sealed record UpdateTeamRequest(string Name, string? City);
