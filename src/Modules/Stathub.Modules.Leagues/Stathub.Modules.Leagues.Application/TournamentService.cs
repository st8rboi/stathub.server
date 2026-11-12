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
    IEnumerable<ICompetitionUsageSource> usageSources)
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
        string name,
        StageFormatType formatType,
        int winPoints,
        int drawPoints,
        int lossPoints,
        int periodDurationMinutes,
        int periodsCount,
        bool extraTimeEnabled,
        bool penaltyShootoutEnabled,
        CancellationToken cancellationToken = default)
    {
        var tournament = await GetOrThrowAsync(leagueId, tournamentId, cancellationToken);

        var pointsRule = new PointsRule(winPoints, drawPoints, lossPoints);
        var matchFormatRule = new MatchFormatRule(periodDurationMinutes, periodsCount, extraTimeEnabled, penaltyShootoutEnabled);

        var stage = tournament.AddStage(name, formatType, pointsRule, matchFormatRule);
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
    /// Удаление турнира вместе с этапами. Только если в нём нет заявок и матчей: они живут
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
        CancellationToken cancellationToken = default)
    {
        var tournament = await GetOrThrowAsync(leagueId, tournamentId, cancellationToken);

        var pointsRule = new PointsRule(winPoints, drawPoints, lossPoints);
        var matchFormatRule = new MatchFormatRule(periodDurationMinutes, periodsCount, extraTimeEnabled, penaltyShootoutEnabled);

        var hasMatches = false;
        foreach (var source in usageSources)
        {
            if (await source.StageHasMatchesAsync(stageId, cancellationToken))
            {
                hasMatches = true;
                break;
            }
        }

        var stage = tournament.UpdateStage(stageId, name, formatType, pointsRule, matchFormatRule, hasMatches);
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
