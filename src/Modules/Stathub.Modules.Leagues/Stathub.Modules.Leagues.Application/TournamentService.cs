using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.Modules.Leagues.Domain.Entities;
using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Modules.Leagues.Domain.ValueObjects;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Application;

public sealed class TournamentService(
    ITournamentRepository repository,
    ILeagueRepository leagueRepository,
    IEnumerable<ICompetitionUsageSource> usageSources,
    IEnumerable<IDivisionTeamSource> divisionTeamSources)
{
    public async Task<Guid> CreateTournamentAsync(
        Guid leagueId,
        string name,
        DateOnly startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        var league = await GetLeagueOrThrowAsync(leagueId, cancellationToken);
        league.EnsureCanAddTournament();

        var tournament = Tournament.Create(leagueId, name, startDate, endDate);

        await repository.AddAsync(tournament, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return tournament.Id;
    }

    public async Task<TournamentDto> GetByIdAsync(
        Guid leagueId,
        Guid tournamentId,
        CancellationToken cancellationToken = default) =>
        TournamentDto.From(await GetOrThrowAsync(leagueId, tournamentId, cancellationToken));

    public async Task<IReadOnlyList<TournamentSummaryDto>> ListByLeagueAsync(
        Guid leagueId,
        CancellationToken cancellationToken = default)
    {
        await GetLeagueOrThrowAsync(leagueId, cancellationToken);

        var tournaments = await repository.ListByLeagueAsync(leagueId, cancellationToken);

        return tournaments.Select(TournamentSummaryDto.From).ToList();
    }

    public async Task<Guid> AddStageAsync(
        Guid leagueId,
        Guid tournamentId,
        Guid? divisionId,
        string name,
        StageFormatType formatType,
        int winPoints,
        int drawPoints,
        int lossPoints,
        int periodDurationMinutes,
        int periodsCount,
        bool extraTimeEnabled,
        bool penaltyShootoutEnabled,
        int? qualifiersCount,
        CancellationToken cancellationToken = default)
    {
        var tournament = await GetOrThrowAsync(leagueId, tournamentId, cancellationToken);

        var pointsRule = new PointsRule(winPoints, drawPoints, lossPoints);
        var matchFormatRule = new MatchFormatRule(periodDurationMinutes, periodsCount, extraTimeEnabled, penaltyShootoutEnabled);

        var stage = tournament.AddStage(
            ResolveDivisionId(tournament, divisionId),
            name,
            formatType,
            pointsRule,
            matchFormatRule,
            qualifiersCount);
        await repository.SaveChangesAsync(cancellationToken);

        return stage.Id;
    }

    public async Task<TournamentDto> UpdateAsync(
        Guid leagueId,
        Guid tournamentId,
        string name,
        DateOnly startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        var tournament = await GetOrThrowAsync(leagueId, tournamentId, cancellationToken);

        tournament.Update(name, startDate, endDate);
        await repository.SaveChangesAsync(cancellationToken);

        return TournamentDto.From(tournament);
    }

    /// <summary>
    /// Удаление турнира вместе с дивизионами и этапами. Только если в нём нет заявок и матчей: они живут
    /// в других модулях, и без FK удаление оставило бы их «висящими».
    /// </summary>
    public async Task DeleteAsync(Guid leagueId, Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var tournament = await GetOrThrowAsync(leagueId, tournamentId, cancellationToken);

        var usages = new List<string>();
        foreach (var source in usageSources)
        {
            var usage = await source.GetTournamentUsageAsync(tournamentId, cancellationToken);
            if (usage is not null)
                usages.Add(usage);
        }

        if (usages.Count > 0)
            throw new ConflictException(
                $"Турнир нельзя удалить: в нём есть данные ({string.Join(", ", usages)}). Его можно отменить.");

        repository.Remove(tournament);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<StageDto> UpdateStageAsync(
        Guid leagueId,
        Guid tournamentId,
        Guid stageId,
        string name,
        StageFormatType formatType,
        int winPoints,
        int drawPoints,
        int lossPoints,
        int periodDurationMinutes,
        int periodsCount,
        bool extraTimeEnabled,
        bool penaltyShootoutEnabled,
        int? qualifiersCount,
        CancellationToken cancellationToken = default)
    {
        var tournament = await GetOrThrowAsync(leagueId, tournamentId, cancellationToken);

        var pointsRule = new PointsRule(winPoints, drawPoints, lossPoints);
        var matchFormatRule = new MatchFormatRule(periodDurationMinutes, periodsCount, extraTimeEnabled, penaltyShootoutEnabled);

        var hasMatches = await StageHasMatchesAsync(stageId, cancellationToken);

        var stage = tournament.UpdateStage(
            stageId, name, formatType, pointsRule, matchFormatRule, qualifiersCount, hasMatches);
        await repository.SaveChangesAsync(cancellationToken);

        return StageDto.From(stage);
    }

    /// <summary>Удаление этапа без заявок и матчей; остальные этапы перенумеровываются.</summary>
    public async Task DeleteStageAsync(
        Guid leagueId,
        Guid tournamentId,
        Guid stageId,
        CancellationToken cancellationToken = default)
    {
        var tournament = await GetOrThrowAsync(leagueId, tournamentId, cancellationToken);

        if (tournament.Stages.All(stage => stage.Id != stageId))
            throw new NotFoundException($"Этап '{stageId}' не найден в турнире '{tournamentId}'.");

        var usages = new List<string>();
        foreach (var source in usageSources)
        {
            var usage = await source.GetStageUsageAsync(stageId, cancellationToken);
            if (usage is not null)
                usages.Add(usage);
        }

        if (usages.Count > 0)
            throw new ConflictException(
                $"Этап нельзя удалить: в нём есть данные ({string.Join(", ", usages)}).");

        tournament.RemoveStage(stageId);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<DivisionDto> AddDivisionAsync(
        Guid leagueId,
        Guid tournamentId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var tournament = await GetOrThrowAsync(leagueId, tournamentId, cancellationToken);

        var division = tournament.AddDivision(name);
        await repository.SaveChangesAsync(cancellationToken);

        return DivisionDto.From(division, tournament.Stages);
    }

    public async Task<DivisionDto> RenameDivisionAsync(
        Guid leagueId,
        Guid tournamentId,
        Guid divisionId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var tournament = await GetOrThrowAsync(leagueId, tournamentId, cancellationToken);

        var division = tournament.RenameDivision(divisionId, name);
        await repository.SaveChangesAsync(cancellationToken);

        return DivisionDto.From(division, tournament.Stages);
    }

    /// <summary>
    /// Удаление дивизиона вместе с этапами — только пока в нём нет заявок, а в его этапах нет матчей.
    /// </summary>
    public async Task DeleteDivisionAsync(
        Guid leagueId,
        Guid tournamentId,
        Guid divisionId,
        CancellationToken cancellationToken = default)
    {
        var tournament = await GetOrThrowAsync(leagueId, tournamentId, cancellationToken);

        if (tournament.Divisions.All(division => division.Id != divisionId))
            throw new NotFoundException($"Дивизион '{divisionId}' не найден в турнире '{tournamentId}'.");

        var usages = new List<string>();
        foreach (var source in usageSources)
        {
            if (await source.GetDivisionUsageAsync(divisionId, cancellationToken) is { } divisionUsage)
                usages.Add(divisionUsage);

            foreach (var stage in tournament.Stages.Where(stage => stage.DivisionId == divisionId))
            {
                if (await source.GetStageUsageAsync(stage.Id, cancellationToken) is { } stageUsage)
                    usages.Add($"{stage.Name} — {stageUsage}");
            }
        }

        if (usages.Count > 0)
            throw new ConflictException(
                $"Дивизион нельзя удалить: в нём есть данные ({string.Join(", ", usages)}).");

        tournament.RemoveDivision(divisionId);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<StageGroupDto> AddStageGroupAsync(
        Guid leagueId,
        Guid tournamentId,
        Guid stageId,
        string name,
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken = default)
    {
        var tournament = await GetOrThrowAsync(leagueId, tournamentId, cancellationToken);
        await EnsureTeamsInDivisionAsync(tournament, stageId, teamIds, cancellationToken);

        var group = tournament.AddStageGroup(
            stageId, name, teamIds, await StageHasMatchesAsync(stageId, cancellationToken));
        await repository.SaveChangesAsync(cancellationToken);

        return StageGroupDto.From(group);
    }

    public async Task<StageGroupDto> UpdateStageGroupAsync(
        Guid leagueId,
        Guid tournamentId,
        Guid stageId,
        Guid groupId,
        string name,
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken = default)
    {
        var tournament = await GetOrThrowAsync(leagueId, tournamentId, cancellationToken);
        await EnsureTeamsInDivisionAsync(tournament, stageId, teamIds, cancellationToken);

        var group = tournament.UpdateStageGroup(
            stageId, groupId, name, teamIds, await StageHasMatchesAsync(stageId, cancellationToken));
        await repository.SaveChangesAsync(cancellationToken);

        return StageGroupDto.From(group);
    }

    public async Task DeleteStageGroupAsync(
        Guid leagueId,
        Guid tournamentId,
        Guid stageId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var tournament = await GetOrThrowAsync(leagueId, tournamentId, cancellationToken);

        tournament.RemoveStageGroup(stageId, groupId, await StageHasMatchesAsync(stageId, cancellationToken));
        await repository.SaveChangesAsync(cancellationToken);
    }

    public Task StartAsync(Guid leagueId, Guid tournamentId, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(leagueId, tournamentId, t => t.Start(), cancellationToken);

    public Task CompleteAsync(Guid leagueId, Guid tournamentId, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(leagueId, tournamentId, t => t.Complete(), cancellationToken);

    public Task CancelAsync(Guid leagueId, Guid tournamentId, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(leagueId, tournamentId, t => t.Cancel(), cancellationToken);

    private async Task ChangeStatusAsync(
        Guid leagueId,
        Guid tournamentId,
        Action<Tournament> transition,
        CancellationToken cancellationToken)
    {
        var tournament = await GetOrThrowAsync(leagueId, tournamentId, cancellationToken);

        transition(tournament);
        await repository.SaveChangesAsync(cancellationToken);
    }

    // Дивизион можно не указывать, пока он в турнире один: простой чемпионат о дивизионах не знает.
    private static Guid ResolveDivisionId(Tournament tournament, Guid? divisionId)
    {
        if (divisionId is { } explicitId)
            return explicitId;

        if (tournament.Divisions.Count == 1)
            return tournament.Divisions.Single().Id;

        throw new DomainException("В турнире несколько дивизионов: укажите, в какой добавить этап.");
    }

    private async Task<bool> StageHasMatchesAsync(Guid stageId, CancellationToken cancellationToken)
    {
        foreach (var source in usageSources)
        {
            if (await source.StageHasMatchesAsync(stageId, cancellationToken))
                return true;
        }

        return false;
    }

    // В группу попадают только команды, заявленные в дивизион этапа: иначе у группы появились бы
    // участники без составов.
    private async Task EnsureTeamsInDivisionAsync(
        Tournament tournament,
        Guid stageId,
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(teamIds);

        var stage = tournament.Stages.FirstOrDefault(item => item.Id == stageId)
            ?? throw new NotFoundException($"Этап '{stageId}' не найден в турнире '{tournament.Id}'.");

        if (teamIds.Count == 0)
            return;

        var registered = new HashSet<Guid>();
        foreach (var source in divisionTeamSources)
            registered.UnionWith(await source.GetTeamIdsAsync(stage.DivisionId, cancellationToken));

        if (teamIds.Any(teamId => !registered.Contains(teamId)))
            throw new ConflictException("В группу можно добавить только команды, заявленные в этот дивизион.");
    }

    private async Task<League> GetLeagueOrThrowAsync(Guid leagueId, CancellationToken cancellationToken) =>
        await leagueRepository.GetByIdAsync(leagueId, cancellationToken)
        ?? throw new NotFoundException($"Лига '{leagueId}' не найдена.");

    // Турнир чужой лиги (несовпадение leagueId в URL) считаем ненайденным
    private async Task<Tournament> GetOrThrowAsync(Guid leagueId, Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await repository.GetByIdAsync(tournamentId, cancellationToken);

        if (tournament is null || tournament.LeagueId != leagueId)
            throw new NotFoundException($"Турнир '{tournamentId}' не найден в лиге '{leagueId}'.");

        return tournament;
    }
}
