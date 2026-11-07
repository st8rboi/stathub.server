namespace Stathub.WebApi.Contracts.Players;

/// <summary>
/// Исправление ФИО и даты рождения игрока. Дата рождения необязательна (null — неизвестна).
/// </summary>
public sealed record UpdatePlayerRequest(string FullName, DateOnly? BirthDate);
