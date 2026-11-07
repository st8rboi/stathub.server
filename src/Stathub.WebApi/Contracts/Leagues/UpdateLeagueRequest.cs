namespace Stathub.WebApi.Contracts.Leagues;

/// <summary>
/// Изменение карточки лиги. Вид спорта и источник данных не меняются.
/// У опубликованной лиги город обязателен.
/// </summary>
public sealed record UpdateLeagueRequest(string Name, string Slug, string? City, string? Region);
