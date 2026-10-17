namespace Stathub.WebApi.Contracts.Players;

/// <summary>
/// Создание игрока. Дата рождения необязательна, но помогает отличить полных тёзок.
/// </summary>
public sealed record CreatePlayerRequest(string FullName, DateOnly? BirthDate);
