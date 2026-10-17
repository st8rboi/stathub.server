namespace Stathub.Modules.Leagues.Application.Dtos;

/// <summary>
/// Сведения о турнире для публичной витрины: только то, что можно показывать зрителю,
/// плюс признак публикации, чтобы витрина не отдавала черновики и архив.
/// </summary>
public sealed record TournamentPublicInfoDto(
    Guid Id,
    string Name,
    Guid LeagueId,
    string LeagueName,
    string LeagueSlug,
    string? LeagueLogoUrl,
    bool IsPublished);
