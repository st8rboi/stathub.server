using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.Modules.Leagues.Domain.Entities;
using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Modules.Leagues.Domain.ValueObjects;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Application;

public sealed class TournamentService(ITournamentRepository repository, ILeagueRepository leagueRepository)
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
        ?? throw new NotFoundException($"League '{leagueId}' was not found.");

    // Турнир чужой лиги (несовпадение leagueId в URL) считаем ненайденным
    private async Task<Tournament> GetOrThrowAsync(Guid leagueId, Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await repository.GetByIdAsync(tournamentId, cancellationToken);

        if (tournament is null || tournament.LeagueId != leagueId)
            throw new NotFoundException($"Tournament '{tournamentId}' was not found in league '{leagueId}'.");

        return tournament;
    }
}
