using Stathub.Modules.Standings.Application.Dtos;
using Stathub.Modules.Statistics.Application.Dtos;

namespace Stathub.WebApi.Contracts.Public;

/// <summary>Публичная карточка лиги — без организатора и внутренних полей.</summary>
public sealed record PublicLeagueDto(
    Guid Id,
    string Name,
    string Slug,
    string Sport,
    string? City,
    string? Region,
    string? LogoUrl,
    string DataSource);

public sealed record PublicStageSummaryDto(
    Guid Id,
    int Order,
    string Name,
    string FormatType);

public sealed record PublicTournamentDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    string Status,
    IReadOnlyList<PublicStageSummaryDto> Stages);

public sealed record PublicLeaguePageDto(
    PublicLeagueDto League,
    IReadOnlyList<PublicTournamentDto> Tournaments);

public sealed record PublicStageDto(
    Guid Id,
    string Name,
    int Order,
    string FormatType,
    string TournamentName,
    Guid TournamentId,
    string LeagueName,
    string LeagueSlug,
    string? LeagueLogoUrl,
    int WinPoints,
    int DrawPoints,
    int LossPoints);

public sealed record PublicMatchDto(
    Guid Id,
    Guid StageId,
    string StageName,
    string TournamentName,
    string LeagueName,
    string LeagueSlug,
    string? LeagueLogoUrl,
    Guid HomeTeamId,
    string HomeTeamName,
    string? HomeTeamLogoUrl,
    Guid AwayTeamId,
    string AwayTeamName,
    string? AwayTeamLogoUrl,
    string Status,
    int HomeScore,
    int AwayScore,
    int CurrentPeriod,
    int PeriodsCount,
    DateTime ScheduledTimeUtc,
    DateTime? StartedAtUtc,
    DateTime? PeriodStartedAtUtc);

public sealed record PublicEventDto(
    Guid Id,
    string Type,
    string? TeamName,
    string? PlayerName,
    DateTime ClientTimestampUtc,
    bool IsVoided,
    string? Reason);

public sealed record PublicMatchPageDto(
    PublicMatchDto Match,
    IReadOnlyList<PublicEventDto> Events);

public sealed record PublicStagePageDto(
    PublicStageDto Stage,
    IReadOnlyList<PublicMatchDto> Matches,
    IReadOnlyList<StandingsRowDto> Standings);

/// <summary>Шапка турнира на странице статистики: лига видна для навигации.</summary>
public sealed record PublicTournamentHeaderDto(
    Guid Id,
    string Name,
    string LeagueName,
    string LeagueSlug,
    string? LeagueLogoUrl);

public sealed record PublicTournamentStatisticsDto(
    PublicTournamentHeaderDto Tournament,
    IReadOnlyList<PlayerStatisticsRowDto> Rows);
