using Stathub.Modules.Leagues.Application;
using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Modules.Matches.Application;
using Stathub.Modules.Matches.Application.Dtos;
using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Modules.Matches.Domain.Services;
using Stathub.Modules.Players.Application;
using Stathub.Modules.Players.Application.Dtos;
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
    IDivisionReader divisionReader,
    PlayerStatisticsService playerStatisticsService,
    RosterService rosterService)
{
    public async Task<IReadOnlyList<PublicLeagueDto>> ListLeaguesAsync(
        string? query,
        string? city,
        CancellationToken cancellationToken)
    {
        var leagues = await leagueService.ListPublishedAsync(query, city, cancellationToken);
        if (leagues.Count < 2)
            return leagues.Select(ToPublicLeague).ToList();

        // Лиги, где сейчас играют, — первыми: иначе каталог, сайдбар и таблица на главной
        // открываются на свежесозданных пустых лигах.
        var activity = await GetLeagueActivityAsync(cancellationToken);

        return leagues
            .Select((league, index) => (league, index, at: activity.GetValueOrDefault(league.Slug)))
            .OrderByDescending(item => item.at)
            .ThenBy(item => item.index)
            .Select(item => ToPublicLeague(item.league))
            .ToList();
    }

    /// <summary>
    /// «Активность» лиги — ближайший к сегодняшнему дню матч её опубликованных турниров:
    /// сыгранные до сегодня и назначенные на ближайшую неделю. Чем свежее, тем выше лига.
    /// </summary>
    private async Task<Dictionary<string, DateTime>> GetLeagueActivityAsync(CancellationToken cancellationToken)
    {
        var horizon = DateTime.UtcNow.AddDays(7);
        var slots = await matchService.ListSlotsAsync(cancellationToken);
        var lastByStage = slots
            .Where(slot => slot.ScheduledTimeUtc <= horizon)
            .GroupBy(slot => slot.StageId)
            .ToDictionary(group => group.Key, group => group.Max(slot => slot.ScheduledTimeUtc));

        var published = new Dictionary<Guid, bool>();
        var activity = new Dictionary<string, DateTime>();

        foreach (var (stageId, lastUtc) in lastByStage)
        {
            var stage = await stagePublicReader.GetAsync(stageId, cancellationToken);
            if (stage is null) continue;
            if (!await IsPublishedAsync(stage.TournamentId, published, cancellationToken)) continue;

            if (!activity.TryGetValue(stage.LeagueSlug, out var known) || lastUtc > known)
                activity[stage.LeagueSlug] = lastUtc;
        }

        return activity;
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
                details.Stages.Select(ToStageSummary).ToList(),
                details.Divisions
                    .Select(division => new PublicDivisionDto(
                        division.Id,
                        division.Order,
                        division.Name,
                        division.Stages.Select(ToStageSummary).ToList()))
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

        var tournament = await tournamentService.GetByIdAsync(stage.LeagueId, stage.TournamentId, cancellationToken);
        var divisionStages = tournament.Divisions
            .Where(division => division.Id == stage.DivisionId)
            .SelectMany(division => division.Stages)
            .Select(ToStageSummary)
            .ToList();

        return new PublicStagePageDto(
            ToPublicStage(stage),
            publicMatches,
            standings.Rows,
            standings.Groups,
            divisionStages);
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

        var players = await GetPlayersAsync(playerIds, cancellationToken);

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

        // Заявка принадлежит дивизиону, матчи — этапам. Участие — это этап: с матчами команды,
        // а если матчей в дивизионе ещё нет — первый этап дивизиона, куда команда заявлена.
        var entriesByDivision = entries
            .GroupBy(entry => entry.DivisionId)
            .ToDictionary(group => group.Key, group => group.First());
        var matchesByStage = teamMatches
            .GroupBy(match => match.StageId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var stageIds = matchesByStage.Keys.ToHashSet();
        var divisionsWithMatches = teamMatches.Select(match => match.DivisionId).ToHashSet();

        foreach (var divisionId in entriesByDivision.Keys.Where(id => !divisionsWithMatches.Contains(id)))
        {
            var division = await divisionReader.GetAsync(divisionId, cancellationToken);
            if (division is { StageIds.Count: > 0 })
                stageIds.Add(division.StageIds[0]);
        }

        var published = new Dictionary<Guid, bool>();
        var participations = new List<(StagePublicInfoDto Stage, DateTime LastActivityUtc)>();

        foreach (var id in stageIds)
        {
            var stage = await stagePublicReader.GetAsync(id, cancellationToken);
            if (stage is null) continue;
            if (!await IsPublishedAsync(stage.TournamentId, published, cancellationToken)) continue;

            // Активность этапа — только начатые матчи: иначе заранее составленный плей-офф
            // «свежее» идущего круга, и команда открывается на пустом этапе без результатов.
            var lastMatchUtc = matchesByStage.TryGetValue(id, out var stageMatches)
                ? stageMatches
                    .Where(match => match.Status is not (MatchStatus.Scheduled or MatchStatus.Cancelled))
                    .Select(match => match.ScheduledTimeUtc)
                    .DefaultIfEmpty(DateTime.MinValue)
                    .Max()
                : DateTime.MinValue;
            var registeredUtc = entriesByDivision.TryGetValue(stage.DivisionId, out var entry)
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
        // Состав и статистика — за весь дивизион: игрок один на круг и плей-офф.
        var players = await GetTeamPlayersAsync(
            teamId,
            selected,
            entriesByDivision.GetValueOrDefault(selected.DivisionId),
            teamMatches.Where(match => match.DivisionId == selected.DivisionId).ToList(),
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
                    entriesByDivision.GetValueOrDefault(item.Stage.DivisionId)?.Status.ToString(),
                    item.Stage.DivisionId,
                    DivisionLabel(item.Stage)))
                .ToList(),
            ToPublicStage(selected),
            matches.Select(match => ToPublicMatch(match, selected, teams)).ToList(),
            standings.Rows,
            players,
            standings.Groups);
    }

    /// <summary>
    /// Состав команды в дивизионе со статистикой за её матчи во всех этапах дивизиона.
    /// Игроки с событиями, но без заявки (заявку удалили как ошибочную), в состав не попадают.
    /// </summary>
    private async Task<IReadOnlyList<PublicTeamPlayerDto>> GetTeamPlayersAsync(
        Guid teamId,
        StagePublicInfoDto stage,
        TournamentTeamDetailsDto? entry,
        IReadOnlyCollection<MatchDto> divisionMatches,
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

        var players = (await GetPlayersAsync(
                registrations.Select(registration => registration.PlayerId).ToList(),
                cancellationToken))
            .ToDictionary(player => player.Id);

        var statistics = (await playerStatisticsService.GetTeamStatisticsAsync(
                stage.DivisionId,
                teamId,
                divisionMatches.Select(match => match.Id).ToList(),
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

    /// <summary>
    /// Статистика игроков — вкладка «Статистика» на витрине. Считается по одному дивизиону турнира:
    /// указанному или, по умолчанию, первому.
    /// </summary>
    public async Task<PublicTournamentStatisticsDto> GetTournamentStatisticsAsync(
        Guid tournamentId,
        Guid? divisionId,
        CancellationToken cancellationToken)
    {
        var info = await tournamentReader.GetPublicInfoAsync(tournamentId, cancellationToken)
            ?? throw new NotFoundException($"Турнир '{tournamentId}' не найден.");

        // Черновики и архивные лиги не показываем: их турниры существуют, но публично скрыты.
        if (!info.IsPublished)
            throw new NotFoundException($"Турнир '{tournamentId}' не опубликован.");

        var divisions = await divisionReader.ListByTournamentAsync(tournamentId, cancellationToken);

        var selected = divisionId is { } requestedDivisionId
            ? divisions.FirstOrDefault(division => division.DivisionId == requestedDivisionId)
              ?? throw new NotFoundException($"Дивизион '{requestedDivisionId}' не найден в этом турнире.")
            : divisions.FirstOrDefault();

        var rows = selected is null
            ? []
            : await playerStatisticsService.GetDivisionStatisticsAsync(selected.DivisionId, cancellationToken);

        // Фото — из модуля Players: Statistics о них не знает.
        var photos = (await GetPlayersAsync(
                rows.Select(row => row.PlayerId).Distinct().ToList(),
                cancellationToken))
            .ToDictionary(player => player.Id, player => player.PhotoUrl);

        return new PublicTournamentStatisticsDto(
            new PublicTournamentHeaderDto(
                info.Id,
                info.Name,
                info.LeagueName,
                info.LeagueSlug,
                info.LeagueLogoUrl),
            rows
                .Select(row => PublicPlayerStatisticsRowDto.From(row, photos.GetValueOrDefault(row.PlayerId)))
                .ToList(),
            selected?.DivisionId,
            divisions
                .Select(division => new PublicDivisionRefDto(division.DivisionId, division.Name, division.Order))
                .ToList());
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

    /// <summary>
    /// Игроки по списку id пачками: у PlayerService лимит на запрос (он рассчитан на админку),
    /// а дивизион или протокол легко набирают больше — 12 команд по 15 человек это уже 180.
    /// </summary>
    private async Task<List<PlayerDto>> GetPlayersAsync(
        IReadOnlyList<Guid> playerIds,
        CancellationToken cancellationToken)
    {
        var players = new List<PlayerDto>(playerIds.Count);

        foreach (var batch in playerIds.Chunk(PlayerService.MaxListLimit))
            players.AddRange(await playerService.GetByIdsAsync(batch, cancellationToken));

        return players;
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
        stage.LossPoints,
        stage.DivisionId,
        stage.DivisionName,
        stage.DivisionsCount,
        stage.QualifiersCount);

    private static PublicStageSummaryDto ToStageSummary(StageDto stage) => new(
        stage.Id,
        stage.Order,
        stage.Name,
        stage.FormatType.ToString(),
        stage.DivisionId);

    // Единственный дивизион турнира зрителю не показываем: это технический уровень.
    private static string? DivisionLabel(StagePublicInfoDto? stage) =>
        stage is { DivisionsCount: > 1 } ? stage.DivisionName : null;

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
            match.PeriodDurationMinutes,
            DivisionLabel(stage));
    }
}
