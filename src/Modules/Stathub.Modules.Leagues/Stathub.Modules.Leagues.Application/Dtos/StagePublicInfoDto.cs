using Stathub.Modules.Leagues.Application.Dtos;

namespace Stathub.Modules.Leagues.Application.Dtos;

/// <summary>
/// Публичный контекст этапа: имена этапа/турнира/лиги и правила таблицы.
/// Одна DTO на все публичные экраны, чтобы витрина не дергала три модуля ради названий.
/// </summary>
public sealed record StagePublicInfoDto(
    Guid StageId,
    string StageName,
    int StageOrder,
    string FormatType,
    Guid TournamentId,
    string TournamentName,
    Guid LeagueId,
    string LeagueName,
    string LeagueSlug,
    string? LeagueLogoUrl,
    int WinPoints,
    int DrawPoints,
    int LossPoints,
    IReadOnlyList<TiebreakerKind> Tiebreakers);
