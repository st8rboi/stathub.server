using Stathub.Modules.Matches.Application.Dtos;
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
    string FormatType,
    Guid DivisionId);

/// <summary>Дивизион турнира с этапами. Если дивизион в турнире один, витрина его не показывает.</summary>
public sealed record PublicDivisionDto(
    Guid Id,
    int Order,
    string Name,
    IReadOnlyList<PublicStageSummaryDto> Stages);

/// <summary>
/// Турнир на странице лиги. <see cref="Divisions"/> — дерево «дивизион → этапы»,
/// <see cref="Stages"/> — те же этапы плоским списком.
/// </summary>
public sealed record PublicTournamentDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    string Status,
    IReadOnlyList<PublicStageSummaryDto> Stages,
    IReadOnlyList<PublicDivisionDto> Divisions);

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
    int LossPoints,
    Guid DivisionId,
    string DivisionName,
    // Сколько дивизионов в турнире: при одном уровень дивизиона на витрине скрыт.
    int DivisionsCount,
    // Сколько первых команд выходит дальше (при группах — из каждой); null — никто.
    int? QualifiersCount);

/// <summary>День с матчами в часовом поясе зрителя — вкладка полосы дат на главной.</summary>
public sealed record PublicMatchDayDto(DateOnly Date, int Count);

public sealed record PublicMatchDto(
    Guid Id,
    // Короткий сквозной номер: ссылка на оверлей трансляции — /overlay.html?match=<Number>.
    int Number,
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
    int PeriodDurationMinutes,
    // Дивизион матча; null, если дивизион в турнире один и показывать его незачем.
    string? DivisionName);

/// <summary>
/// Событие протокола для витрины. <see cref="Period"/> и <see cref="ElapsedSeconds"/> — игровое
/// время без пауз (null у событий жизненного цикла); <see cref="TargetEventId"/> — у отмены.
/// <see cref="PlayerPhotoUrl"/> — фото игрока для плашек трансляционного оверлея.
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
    int? ElapsedSeconds,
    string? PlayerPhotoUrl);

/// <summary>
/// Страница матча. <see cref="Statistics"/> — командная статистика от статистика;
/// null, если по матчу её не вели (вкладку «Статистика» тогда не показываем).
/// </summary>
public sealed record PublicMatchPageDto(
    PublicMatchDto Match,
    IReadOnlyList<PublicEventDto> Events,
    MatchStatisticsDto? Statistics);

/// <summary>
/// Страница этапа. <see cref="StandingsGroups"/> заполнен у группового этапа (тогда
/// <see cref="Standings"/> — те же строки подряд); <see cref="DivisionStages"/> — этапы
/// того же дивизиона для переключателя «круг / плей-офф».
/// </summary>
public sealed record PublicStagePageDto(
    PublicStageDto Stage,
    IReadOnlyList<PublicMatchDto> Matches,
    IReadOnlyList<StandingsRowDto> Standings,
    IReadOnlyList<StandingsGroupDto> StandingsGroups,
    IReadOnlyList<PublicStageSummaryDto> DivisionStages);

/// <summary>Шапка турнира на странице статистики: лига видна для навигации.</summary>
public sealed record PublicTournamentHeaderDto(
    Guid Id,
    string Name,
    string LeagueName,
    string LeagueSlug,
    string? LeagueLogoUrl);

/// <summary>Дивизион в переключателе статистики.</summary>
public sealed record PublicDivisionRefDto(Guid Id, string Name, int Order);

/// <summary>
/// Строка статистики игрока на витрине: строка модуля Statistics плюс фото игрока из модуля Players.
/// </summary>
public sealed record PublicPlayerStatisticsRowDto(
    Guid PlayerId,
    string PlayerName,
    string? PhotoUrl,
    Guid? TeamId,
    string? TeamName,
    string? TeamLogoUrl,
    int Matches,
    int Goals,
    int Assists,
    int OwnGoals,
    int YellowCards,
    int RedCards)
{
    public static PublicPlayerStatisticsRowDto From(PlayerStatisticsRowDto row, string? photoUrl) => new(
        row.PlayerId,
        row.PlayerName,
        photoUrl,
        row.TeamId,
        row.TeamName,
        row.TeamLogoUrl,
        row.Matches,
        row.Goals,
        row.Assists,
        row.OwnGoals,
        row.YellowCards,
        row.RedCards);
}

/// <summary>
/// Статистика игроков одного дивизиона турнира (<see cref="DivisionId"/>): дивизионы — независимые
/// соревнования, общий список бомбардиров на турнир не строится. <see cref="Divisions"/> — все
/// дивизионы турнира для переключателя.
/// </summary>
public sealed record PublicTournamentStatisticsDto(
    PublicTournamentHeaderDto Tournament,
    IReadOnlyList<PublicPlayerStatisticsRowDto> Rows,
    Guid? DivisionId,
    IReadOnlyList<PublicDivisionRefDto> Divisions);

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
    string? Status,
    Guid DivisionId,
    // null, если дивизион в турнире один
    string? DivisionName);

/// <summary>
/// Игрок заявки команды в дивизион со статистикой за матчи команды во всех этапах дивизиона.
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
/// (по умолчанию — самому свежему): матчи и таблица этапа, состав дивизиона со статистикой.
/// </summary>
public sealed record PublicTeamPageDto(
    PublicTeamDto Team,
    IReadOnlyList<PublicTeamParticipationDto> Participations,
    PublicStageDto Stage,
    IReadOnlyList<PublicMatchDto> Matches,
    IReadOnlyList<StandingsRowDto> Standings,
    IReadOnlyList<PublicTeamPlayerDto> Players,
    IReadOnlyList<StandingsGroupDto> StandingsGroups);
