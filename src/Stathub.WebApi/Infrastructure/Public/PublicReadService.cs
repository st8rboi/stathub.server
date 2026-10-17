using Stathub.Modules.Leagues.Application;
using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Modules.Matches.Application;
using Stathub.Modules.Matches.Application.Dtos;
using Stathub.Modules.Players.Application;
using Stathub.Modules.Standings.Application;
using Stathub.Modules.Statistics.Application;
using Stathub.Modules.Teams.Application;
using Stathub.Modules.Teams.Application.Dtos;
using Stathub.Shared.Exceptions;
using Stathub.WebApi.Contracts.Public;

namespace Stathub.WebApi.Infrastructure.Public;

/// <summary>
/// Композиция публичной витрины: собирает ответы из модулей (лиги, матчи, таблица, команды)
/// в одном месте, чтобы контроллеры остались тонким маппингом HTTP.
/// Это read-only BFF-слой — здесь нет бизнес-правил, только склейка и переименование полей.
/// </summary>
public sealed class PublicReadService(
    LeagueService leagueService,
    TournamentService tournamentService,
    IStagePublicReader stagePublicReader,
    MatchService matchService,
    MatchEventService matchEventService,
    StandingsService standingsService,
    TeamService teamService,
    PlayerService playerService,
    ITournamentReader tournamentReader,
    PlayerStatisticsService playerStatisticsService)
{
    public async Task<IReadOnlyList<PublicLeagueDto>> ListLeaguesAsync(
        string? query,
        string? city,
        CancellationToken cancellationToken)
    {
        var leagues = await leagueService.ListPublishedAsync(query, city, cancellationToken);
        return leagues.Select(ToPublicLeague).ToList();
    }

    public async Task<PublicLeaguePageDto> GetLeaguePageAsync(string slug, CancellationToken cancellationToken)
    {
        var league = await leagueService.GetBySlugAsync(slug, cancellationToken);

        // Публично видно только опубликованные лиги: черновики и архив не должны всплывать (doc §2.1).
        if (league.Status != LeagueStatus.Published)
            throw new NotFoundException($"Лига '{slug}' не опубликована.");

        var summaries = await tournamentService.ListByLeagueAsync(league.Id, cancellationToken);
        var tournaments = new List<PublicTournamentDto>(summaries.Count);

        foreach (var summary in summaries)
        {
            var details = await tournamentService.GetByIdAsync(league.Id, summary.Id, cancellationToken);

            tournaments.Add(new PublicTournamentDto(
                details.Id,
                details.Name,
                details.StartDate,
                details.EndDate,
                details.Status.ToString(),
                details.Stages
                    .Select(stage => new PublicStageSummaryDto(
                        stage.Id,
                        stage.Order,
                        stage.Name,
                        stage.FormatType.ToString()))
                    .ToList()));
        }

        return new PublicLeaguePageDto(ToPublicLeague(league), tournaments);
    }

    public async Task<PublicStagePageDto> GetStagePageAsync(Guid stageId, CancellationToken cancellationToken)
    {
        var stage = await stagePublicReader.GetAsync(stageId, cancellationToken)
            ?? throw new NotFoundException($"Этап '{stageId}' не найден.");

        var matches = await matchService.ListByStageAsync(stageId, cancellationToken);
        var teams = await GetTeamsAsync(CollectTeamIds(matches), cancellationToken);

        var publicMatches = matches
            .Select(match => ToPublicMatch(match, stage, teams))
            .ToList();

        var standings = await standingsService.GetStageStandingsAsync(stageId, cancellationToken);

        return new PublicStagePageDto(ToPublicStage(stage), publicMatches, standings.Rows);
    }

    public async Task<PublicMatchPageDto> GetMatchPageAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var state = await matchEventService.GetStateAsync(matchId, cancellationToken);
        var stage = await stagePublicReader.GetAsync(state.Match.StageId, cancellationToken);

        var teams = await GetTeamsAsync(
            [state.Match.HomeTeamId, state.Match.AwayTeamId],
            cancellationToken);

        var playerIds = state.Events
            .Where(matchEvent => matchEvent.PlayerId.HasValue)
            .Select(matchEvent => matchEvent.PlayerId!.Value)
            .Distinct()
            .ToList();

        var players = playerIds.Count > 0
            ? await playerService.GetByIdsAsync(playerIds, cancellationToken)
            : [];

        var playerNames = players.ToDictionary(player => player.Id, player => player.FullName);

        var events = state.Events
            .Select(matchEvent => new PublicEventDto(
                matchEvent.Id,
                matchEvent.Type.ToString(),
                matchEvent.TeamId is { } teamId ? teams.GetValueOrDefault(teamId)?.Name : null,
                matchEvent.PlayerId is { } playerId ? playerNames.GetValueOrDefault(playerId) : null,
                matchEvent.ClientTimestampUtc,
                matchEvent.IsVoided,
                matchEvent.Reason))
            .ToList();

        return new PublicMatchPageDto(ToPublicMatch(state.Match, stage, teams), events);
    }

    /// <summary>Лента «сейчас играют» для главного экрана витрины (doc §2.1).</summary>
    public async Task<IReadOnlyList<PublicMatchDto>> ListLiveAsync(CancellationToken cancellationToken)
    {
        var matches = await matchService.ListLiveAsync(cancellationToken);
        if (matches.Count == 0) return [];

        var stages = await LoadStagesAsync(matches, cancellationToken);
        var teams = await GetTeamsAsync(CollectTeamIds(matches), cancellationToken);

        return matches
            .Select(match => ToPublicMatch(match, stages.GetValueOrDefault(match.StageId), teams))
            .ToList();
    }

    /// <summary>Расписание на интервал — основа полосы дат и списка соревнований.</summary>
    public async Task<IReadOnlyList<PublicMatchDto>> ListMatchesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var matches = await matchService.ListByPeriodAsync(fromUtc, toUtc, cancellationToken);
        if (matches.Count == 0) return [];

        var stages = await LoadStagesAsync(matches, cancellationToken);
        var teams = await GetTeamsAsync(CollectTeamIds(matches), cancellationToken);

        return matches
            .Select(match => ToPublicMatch(match, stages.GetValueOrDefault(match.StageId), teams))
            .ToList();
    }

    /// <summary>Статистика игроков по турниру — вкладка «Статистика» на витрине.</summary>
    public async Task<PublicTournamentStatisticsDto> GetTournamentStatisticsAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var info = await tournamentReader.GetPublicInfoAsync(tournamentId, cancellationToken)
            ?? throw new NotFoundException($"Турнир '{tournamentId}' не найден.");

        // Черновики и архивные лиги не показываем: их турниры существуют, но публично скрыты.
        if (!info.IsPublished)
            throw new NotFoundException($"Турнир '{tournamentId}' не опубликован.");

        var rows = await playerStatisticsService.GetTournamentStatisticsAsync(tournamentId, cancellationToken);

        return new PublicTournamentStatisticsDto(
            new PublicTournamentHeaderDto(
                info.Id,
                info.Name,
                info.LeagueName,
                info.LeagueSlug,
                info.LeagueLogoUrl),
            rows);
    }

    private async Task<Dictionary<Guid, StagePublicInfoDto?>> LoadStagesAsync(
        IReadOnlyCollection<MatchDto> matches,
        CancellationToken cancellationToken)
    {
        var stages = new Dictionary<Guid, StagePublicInfoDto?>();

        foreach (var stageId in matches.Select(match => match.StageId).Distinct())
            stages[stageId] = await stagePublicReader.GetAsync(stageId, cancellationToken);

        return stages;
    }

    private static List<Guid> CollectTeamIds(IEnumerable<MatchDto> matches) =>
        matches
            .SelectMany(match => new[] { match.HomeTeamId, match.AwayTeamId })
            .Distinct()
            .ToList();

    private async Task<IReadOnlyDictionary<Guid, TeamDto>> GetTeamsAsync(
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken)
    {
        if (teamIds.Count == 0)
            return new Dictionary<Guid, TeamDto>();

        var teams = await teamService.GetByIdsAsync(teamIds, cancellationToken);
        return teams.ToDictionary(team => team.Id);
    }

    private static PublicLeagueDto ToPublicLeague(LeagueDto league) => new(
        league.Id,
        league.Name,
        league.Slug,
        league.Sport.ToString(),
        league.City,
        league.Region,
        league.LogoUrl,
        league.DataSource.ToString());

    private static PublicStageDto ToPublicStage(StagePublicInfoDto stage) => new(
        stage.StageId,
        stage.StageName,
        stage.StageOrder,
        stage.FormatType,
        stage.TournamentName,
        stage.TournamentId,
        stage.LeagueName,
        stage.LeagueSlug,
        stage.LeagueLogoUrl,
        stage.WinPoints,
        stage.DrawPoints,
        stage.LossPoints);

    private static PublicMatchDto ToPublicMatch(
        MatchDto match,
        StagePublicInfoDto? stage,
        IReadOnlyDictionary<Guid, TeamDto> teams)
    {
        var home = teams.GetValueOrDefault(match.HomeTeamId);
        var away = teams.GetValueOrDefault(match.AwayTeamId);

        return new PublicMatchDto(
            match.Id,
            match.StageId,
            stage?.StageName ?? string.Empty,
            stage?.TournamentName ?? string.Empty,
            stage?.LeagueName ?? string.Empty,
            stage?.LeagueSlug ?? string.Empty,
            stage?.LeagueLogoUrl,
            match.HomeTeamId,
            home?.Name ?? "Хозяева",
            home?.LogoUrl,
            match.AwayTeamId,
            away?.Name ?? "Гости",
            away?.LogoUrl,
            match.Status.ToString(),
            match.HomeScore,
            match.AwayScore,
            match.CurrentPeriod,
            match.PeriodsCount,
            match.ScheduledTimeUtc,
            match.StartedAtUtc,
            match.PeriodStartedAtUtc);
    }
}
