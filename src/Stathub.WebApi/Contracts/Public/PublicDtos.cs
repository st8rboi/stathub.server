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
    DateTime? PeriodStartedAtUtc,
    DateTime? PausedAtUtc,
    long PeriodPausedMilliseconds,
    // Длительность периода по регламенту матча: витрина считает минуту как «26'» во втором тайме.
    int PeriodDurationMinutes);

/// <summary>
/// Событие протокола для витрины. <see cref="Period"/> и <see cref="ElapsedSeconds"/> — игровое
/// время без пауз (null у событий жизненного цикла); <see cref="TargetEventId"/> — у отмены.
/// </summary>
public sealed record PublicEventDto(
    Guid Id,
    string Type,
    string? TeamName,
    string? PlayerName,
    DateTime OccurredAtUtc,
    bool IsVoided,
    string? Reason,
    Guid? TeamId,
    string? RelatedPlayerName,
    Guid? TargetEventId,
    int? Period,
    int? ElapsedSeconds);

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

/// <summary>Шапка страницы команды.</summary>
public sealed record PublicTeamDto(
    Guid Id,
    string Name,
    string? City,
    string? LogoUrl);

/// <summary>
/// Участие команды в этапе опубликованной лиги — пункт переключателя на странице команды.
/// <see cref="Status"/> — статус заявки (Active/Withdrawn), null если заявки нет, а матчи есть.
/// </summary>
public sealed record PublicTeamParticipationDto(
    Guid StageId,
    string StageName,
    Guid TournamentId,
    string TournamentName,
    string LeagueName,
    string LeagueSlug,
    string? LeagueLogoUrl,
    string? Status);

/// <summary>
/// Игрок заявки команды на этап со статистикой за матчи команды в этом этапе.
/// Отзаявленные и ушедшие трансфером остаются в списке: история не скрывается.
/// </summary>
public sealed record PublicTeamPlayerDto(
    Guid PlayerId,
    string FullName,
    string? PhotoUrl,
    DateOnly? BirthDate,
    string Status,
    DateOnly JoinedOn,
    DateOnly? LeftOn,
    int Matches,
    int Goals,
    int Assists,
    int OwnGoals,
    int YellowCards,
    int RedCards);

/// <summary>
/// Страница команды: участия во всех опубликованных этапах и подробности по выбранному
/// (по умолчанию — самому свежему): матчи, таблица этапа, состав со статистикой.
/// </summary>
public sealed record PublicTeamPageDto(
    PublicTeamDto Team,
    IReadOnlyList<PublicTeamParticipationDto> Participations,
    PublicStageDto Stage,
    IReadOnlyList<PublicMatchDto> Matches,
    IReadOnlyList<StandingsRowDto> Standings,
    IReadOnlyList<PublicTeamPlayerDto> Players);
