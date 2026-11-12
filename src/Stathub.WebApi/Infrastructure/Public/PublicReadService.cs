using Stathub.Modules.Leagues.Application;
using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Modules.Matches.Application;
using Stathub.Modules.Matches.Application.Dtos;
using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Modules.Matches.Domain.Services;
using Stathub.Modules.Players.Application;
using Stathub.Modules.Rosters.Application;
using Stathub.Modules.Rosters.Application.Dtos;
using Stathub.Modules.Rosters.Domain.Enums;
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
    PlayerStatisticsService playerStatisticsService,
    RosterService rosterService)
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

        // Публично виден только этап опубликованной лиги: черновики и архив не показываем.
        await EnsurePublishedAsync(stage.TournamentId, new Dictionary<Guid, bool>(), cancellationToken);

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

        if (stage is null)
            throw new NotFoundException($"Матч '{matchId}' не найден.");

        await EnsurePublishedAsync(stage.TournamentId, new Dictionary<Guid, bool>(), cancellationToken);

        var teams = await GetTeamsAsync(
            [state.Match.HomeTeamId, state.Match.AwayTeamId],
            cancellationToken);

        var playerIds = state.Events
            .SelectMany(matchEvent => new[] { matchEvent.PlayerId, matchEvent.RelatedPlayerId })
            .OfType<Guid>()
            .Distinct()
            .ToList();

        var players = playerIds.Count > 0
            ? await playerService.GetByIdsAsync(playerIds, cancellationToken)
            : [];

        var playerNames = players.ToDictionary(player => player.Id, player => player.FullName);

        // Часы считаются по полному логу (с паузами), поэтому до фильтрации.
        var clock = MatchClock.Compute(state.Events
            .Select(matchEvent => (matchEvent.Type, matchEvent.OccurredAtUtc))
            .ToList());

        // Паузы внутри периода — служебная деталь судьи, зрителю они видны по стоящим часам.
        var events = state.Events
            .Select((matchEvent, index) => (matchEvent, mark: clock[index]))
            .Where(item => item.matchEvent.Type is not (MatchEventType.MatchPaused or MatchEventType.MatchResumed))
            .Select(item => new PublicEventDto(
                item.matchEvent.Id,
                item.matchEvent.Type.ToString(),
                item.matchEvent.TeamId is { } teamId ? teams.GetValueOrDefault(teamId)?.Name : null,
                item.matchEvent.PlayerId is { } playerId ? playerNames.GetValueOrDefault(playerId) : null,
                item.matchEvent.OccurredAtUtc,
                item.matchEvent.IsVoided,
                item.matchEvent.Reason,
                item.matchEvent.TeamId,
                item.matchEvent.RelatedPlayerId is { } relatedId ? playerNames.GetValueOrDefault(relatedId) : null,
                item.matchEvent.TargetEventId,
                item.mark?.Period,
                item.mark?.ElapsedSeconds))
            .ToList();

        return new PublicMatchPageDto(ToPublicMatch(state.Match, stage, teams), events);
    }

    /// <summary>
    /// Страница команды. Команда общая для всех организаторов, поэтому видна только через
    /// этапы опубликованных лиг: заявки и матчи из черновиков и архива не показываются,
    /// а команда без публичных участий отдаёт 404.
    /// </summary>
    public async Task<PublicTeamPageDto> GetTeamPageAsync(
        Guid teamId,
        Guid? stageId,
        CancellationToken cancellationToken)
    {
        var team = await teamService.GetByIdAsync(teamId, cancellationToken);
        var entries = await rosterService.ListByTeamAsync(teamId, cancellationToken);
        var teamMatches = await matchService.ListByTeamAsync(teamId, cancellationToken);

        // Этап может быть известен только по заявке (матчей ещё нет) или только по матчам
        // (матч без заявки в старых данных) — участием считаем и то, и другое.
        var entriesByStage = entries
            .GroupBy(entry => entry.StageId)
            .ToDictionary(group => group.Key, group => group.First());
        var matchesByStage = teamMatches
            .GroupBy(match => match.StageId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var published = new Dictionary<Guid, bool>();
        var participations = new List<(StagePublicInfoDto Stage, DateTime LastActivityUtc)>();

        foreach (var id in entriesByStage.Keys.Union(matchesByStage.Keys))
        {
            var stage = await stagePublicReader.GetAsync(id, cancellationToken);
            if (stage is null) continue;
            if (!await IsPublishedAsync(stage.TournamentId, published, cancellationToken)) continue;

            var lastMatchUtc = matchesByStage.TryGetValue(id, out var stageMatches)
                ? stageMatches.Max(match => match.ScheduledTimeUtc)
                : DateTime.MinValue;
            var registeredUtc = entriesByStage.TryGetValue(id, out var entry)
                ? entry.CreatedAtUtc
                : DateTime.MinValue;

            participations.Add((stage, lastMatchUtc > registeredUtc ? lastMatchUtc : registeredUtc));
        }

        if (participations.Count == 0)
            throw new NotFoundException($"Команда '{teamId}' не найдена.");

        // Свежие участия первыми: по умолчанию открывается текущий сезон команды.
        participations = participations
            .OrderByDescending(item => item.LastActivityUtc)
            .ThenBy(item => item.Stage.StageOrder)
            .ToList();

        var selected = stageId is { } requestedStageId
            ? participations.Select(item => item.Stage).FirstOrDefault(stage => stage.StageId == requestedStageId)
              ?? throw new NotFoundException($"Команда не участвует в этапе '{requestedStageId}'.")
            : participations[0].Stage;

        var matches = matchesByStage.GetValueOrDefault(selected.StageId) ?? [];
        var teams = await GetTeamsAsync(CollectTeamIds(matches), cancellationToken);
        var standings = await standingsService.GetStageStandingsAsync(selected.StageId, cancellationToken);
        var players = await GetTeamPlayersAsync(
            teamId,
            selected,
            entriesByStage.GetValueOrDefault(selected.StageId),
            matches,
            cancellationToken);

        return new PublicTeamPageDto(
            new PublicTeamDto(team.Id, team.Name, team.City, team.LogoUrl),
            participations
                .Select(item => new PublicTeamParticipationDto(
                    item.Stage.StageId,
                    item.Stage.StageName,
                    item.Stage.TournamentId,
                    item.Stage.TournamentName,
                    item.Stage.LeagueName,
                    item.Stage.LeagueSlug,
                    item.Stage.LeagueLogoUrl,
                    entriesByStage.GetValueOrDefault(item.Stage.StageId)?.Status.ToString()))
                .ToList(),
            ToPublicStage(selected),
            matches.Select(match => ToPublicMatch(match, selected, teams)).ToList(),
            standings.Rows,
            players);
    }

    /// <summary>
    /// Состав команды на этапе со статистикой только за её матчи этого этапа.
    /// Игроки с событиями, но без заявки (заявку удалили как ошибочную), в состав не попадают.
    /// </summary>
    private async Task<IReadOnlyList<PublicTeamPlayerDto>> GetTeamPlayersAsync(
        Guid teamId,
        StagePublicInfoDto stage,
        TournamentTeamDetailsDto? entry,
        IReadOnlyCollection<MatchDto> stageMatches,
        CancellationToken cancellationToken)
    {
        if (entry is null || entry.Registrations.Count == 0)
            return [];

        // У игрока может быть несколько заявок в одной команде (отзаявили и вернули) —
        // показываем последнюю: она отражает текущий статус.
        var registrations = entry.Registrations
            .GroupBy(registration => registration.PlayerId)
            .Select(group => group.OrderBy(registration => registration.JoinedOn).Last())
            .ToList();

        var players = (await playerService.GetByIdsAsync(
                registrations.Select(registration => registration.PlayerId).ToList(),
                cancellationToken))
            .ToDictionary(player => player.Id);

        var statistics = (await playerStatisticsService.GetTeamStatisticsAsync(
                stage.TournamentId,
                teamId,
                stageMatches.Select(match => match.Id).ToList(),
                cancellationToken))
            .ToDictionary(row => row.PlayerId);

        return registrations
            .Select(registration =>
            {
                var player = players.GetValueOrDefault(registration.PlayerId);
                var row = statistics.GetValueOrDefault(registration.PlayerId);

                return new PublicTeamPlayerDto(
                    registration.PlayerId,
                    player?.FullName ?? "Игрок",
                    player?.PhotoUrl,
                    player?.BirthDate,
                    registration.Status.ToString(),
                    registration.JoinedOn,
                    registration.LeftOn,
                    row?.Matches ?? 0,
                    row?.Goals ?? 0,
                    row?.Assists ?? 0,
                    row?.OwnGoals ?? 0,
                    row?.YellowCards ?? 0,
                    row?.RedCards ?? 0);
            })
            .OrderBy(player => player.Status == nameof(PlayerRegistrationStatus.Active) ? 0 : 1)
            .ThenBy(player => player.FullName, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>Лента «сейчас играют» для главного экрана витрины (doc §2.1).</summary>
    public async Task<IReadOnlyList<PublicMatchDto>> ListLiveAsync(CancellationToken cancellationToken)
    {
        var matches = await matchService.ListLiveAsync(cancellationToken);
        if (matches.Count == 0) return [];

        var stages = await LoadStagesAsync(matches, cancellationToken);
        var published = new Dictionary<Guid, bool>();
        var visible = new List<MatchDto>(matches.Count);

        foreach (var match in matches)
        {
            var stage = stages.GetValueOrDefault(match.StageId);
            if (stage is null) continue;
            if (!await IsPublishedAsync(stage.TournamentId, published, cancellationToken)) continue;

            visible.Add(match);
        }

        if (visible.Count == 0) return [];

        var teams = await GetTeamsAsync(CollectTeamIds(visible), cancellationToken);

        return visible
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
        var published = new Dictionary<Guid, bool>();
        var visible = new List<MatchDto>(matches.Count);

        foreach (var match in matches)
        {
            var stage = stages.GetValueOrDefault(match.StageId);

            // Матч без этапа (старые тестовые данные) и матч неопубликованной лиги
            // в публичное расписание не попадают — иначе полоса дат считает одно,
            // а список показывает другое.
            if (stage is null) continue;
            if (!await IsPublishedAsync(stage.TournamentId, published, cancellationToken)) continue;

            visible.Add(match);
        }

        if (visible.Count == 0) return [];

        var teams = await GetTeamsAsync(CollectTeamIds(visible), cancellationToken);

        return visible
            .Select(match => ToPublicMatch(match, stages.GetValueOrDefault(match.StageId), teams))
            .ToList();
    }

    /// <summary>
    /// Дни, в которые есть матчи опубликованных лиг, с числом матчей. Граница дня — по
    /// часовому поясу зрителя (IANA, например «Europe/Moscow»), иначе вечерний матч
    /// попадёт не в тот день. Фильтр публикации тот же, что у <see cref="ListMatchesAsync"/>,
    /// чтобы счётчик на вкладке совпадал со списком матчей дня.
    /// </summary>
    public async Task<IReadOnlyList<PublicMatchDayDto>> ListMatchDaysAsync(
        string? timeZone,
        CancellationToken cancellationToken)
    {
        var zone = ResolveTimeZone(timeZone);
        var slots = await matchService.ListSlotsAsync(cancellationToken);
        if (slots.Count == 0) return [];

        var published = new Dictionary<Guid, bool>();
        var visibleStages = new HashSet<Guid>();

        foreach (var stageId in slots.Select(slot => slot.StageId).Distinct())
        {
            var stage = await stagePublicReader.GetAsync(stageId, cancellationToken);
            if (stage is null) continue;
            if (!await IsPublishedAsync(stage.TournamentId, published, cancellationToken)) continue;

            visibleStages.Add(stageId);
        }

        return slots
            .Where(slot => visibleStages.Contains(slot.StageId))
            .GroupBy(slot => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(slot.ScheduledTimeUtc, DateTimeKind.Utc),
                zone)))
            .OrderBy(group => group.Key)
            .Select(group => new PublicMatchDayDto(group.Key, group.Count()))
            .ToList();
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new DomainException($"Неизвестный часовой пояс '{timeZone}'.");
        }
    }

    /// <summary>Опубликована ли лига турнира; результат кэшируется на время одного запроса.</summary>
    private async Task<bool> IsPublishedAsync(
        Guid tournamentId,
        Dictionary<Guid, bool> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(tournamentId, out var known))
            return known;

        var info = await tournamentReader.GetPublicInfoAsync(tournamentId, cancellationToken);
        var published = info?.IsPublished == true;
        cache[tournamentId] = published;

        return published;
    }

    private async Task EnsurePublishedAsync(
        Guid tournamentId,
        Dictionary<Guid, bool> cache,
        CancellationToken cancellationToken)
    {
        if (!await IsPublishedAsync(tournamentId, cache, cancellationToken))
            throw new NotFoundException("Турнир не опубликован.");
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
            match.PeriodStartedAtUtc,
            match.PausedAtUtc,
            match.PeriodPausedMilliseconds,
            match.PeriodDurationMinutes);
    }
}
