namespace Stathub.Modules.Leagues.Application.Dtos;

/// <summary>
/// Публичный контекст этапа: имена этапа/дивизиона/турнира/лиги и правила таблицы.
/// Одна DTO на все публичные экраны, чтобы витрина не дергала три модуля ради названий.
/// </summary>
/// <param name="DivisionsCount">
/// Сколько дивизионов в турнире: при одном интерфейс уровень дивизиона не показывает.
/// </param>
/// <param name="QualifiersCount">Сколько первых команд выходит дальше (при группах — из каждой).</param>
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
    IReadOnlyList<TiebreakerKind> Tiebreakers,
    Guid DivisionId,
    string DivisionName,
    int DivisionOrder,
    int DivisionsCount,
    int? QualifiersCount,
    IReadOnlyList<StageGroupTeamsDto> Groups);
