namespace Stathub.WebApi.Contracts.Teams;

/// <summary>
/// Создание команды (клуба). Город необязателен.
/// </summary>
public sealed record CreateTeamRequest(string Name, string? City);
