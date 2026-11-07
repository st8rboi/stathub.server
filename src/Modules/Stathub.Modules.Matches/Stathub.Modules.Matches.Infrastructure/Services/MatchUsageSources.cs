using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Matches.Infrastructure.Persistence;
using Stathub.Modules.Players.Application.Dtos;
using Stathub.Modules.Players.Application.Interfaces;
using Stathub.Modules.Rosters.Application.Interfaces;
using Stathub.Modules.Teams.Application.Dtos;
using Stathub.Modules.Teams.Application.Interfaces;

namespace Stathub.Modules.Matches.Infrastructure.Services;

/// <summary>Матчи команды — для проверки перед правкой и удалением команды.</summary>
internal sealed class MatchTeamUsageSource(
    MatchesDbContext dbContext,
    ITournamentReader tournamentReader) : ITeamUsageSource
{
    public async Task<TeamUsageDto?> GetUsageAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var tournamentIds = await dbContext.Matches
            .AsNoTracking()
            .Where(match => match.HomeTeamId == teamId || match.AwayTeamId == teamId)
            .Select(match => match.TournamentId)
            .ToListAsync(cancellationToken);

        if (tournamentIds.Count == 0)
            return null;

        var organizerIds = await tournamentReader.GetOrganizerIdsAsync(tournamentIds, cancellationToken);

        return new TeamUsageDto($"матчи: {tournamentIds.Count}", organizerIds);
    }
}

/// <summary>События матчей с участием игрока — для проверки перед правкой и удалением игрока.</summary>
internal sealed class MatchPlayerUsageSource(
    MatchesDbContext dbContext,
    ITournamentReader tournamentReader) : IPlayerUsageSource
{
    public async Task<PlayerUsageDto?> GetUsageAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        // Отменённые события тоже считаются: лог матча неизменяем, ссылка на игрока в нём остаётся.
        var matches = await dbContext.MatchEvents
            .AsNoTracking()
            .Where(matchEvent => matchEvent.PlayerId == playerId || matchEvent.RelatedPlayerId == playerId)
            .Join(
                dbContext.Matches,
                matchEvent => matchEvent.MatchId,
                match => match.Id,
                (_, match) => new { match.Id, match.TournamentId })
            .Distinct()
            .ToListAsync(cancellationToken);

        if (matches.Count == 0)
            return null;

        var organizerIds = await tournamentReader.GetOrganizerIdsAsync(
            matches.Select(match => match.TournamentId).ToList(),
            cancellationToken);

        return new PlayerUsageDto($"матчи с его событиями: {matches.Count}", organizerIds);
    }
}

/// <summary>Матчи турнира и этапа — для проверки перед удалением или сменой правил этапа.</summary>
internal sealed class MatchCompetitionUsageSource(MatchesDbContext dbContext) : ICompetitionUsageSource
{
    public async Task<string?> GetTournamentUsageAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var count = await dbContext.Matches.CountAsync(match => match.TournamentId == tournamentId, cancellationToken);

        return count > 0 ? $"матчи: {count}" : null;
    }

    public async Task<string?> GetStageUsageAsync(Guid stageId, CancellationToken cancellationToken = default)
    {
        var count = await dbContext.Matches.CountAsync(match => match.StageId == stageId, cancellationToken);

        return count > 0 ? $"матчи: {count}" : null;
    }

    public Task<bool> StageHasMatchesAsync(Guid stageId, CancellationToken cancellationToken = default) =>
        dbContext.Matches.AnyAsync(match => match.StageId == stageId, cancellationToken);
}

/// <summary>Матчи и события команды на этапе — для проверки перед удалением заявок.</summary>
internal sealed class MatchRosterUsageSource(MatchesDbContext dbContext) : IRosterUsageSource
{
    public Task<int> CountTeamMatchesAsync(Guid stageId, Guid teamId, CancellationToken cancellationToken = default) =>
        dbContext.Matches.CountAsync(
            match => match.StageId == stageId && (match.HomeTeamId == teamId || match.AwayTeamId == teamId),
            cancellationToken);

    // Отменённые события тоже считаются: лог матча неизменяем, ссылка на игрока в нём остаётся.
    public Task<bool> PlayerHasEventsAsync(
        Guid stageId,
        Guid teamId,
        Guid playerId,
        CancellationToken cancellationToken = default) =>
        dbContext.MatchEvents.AnyAsync(
            matchEvent => matchEvent.TeamId == teamId
                          && (matchEvent.PlayerId == playerId || matchEvent.RelatedPlayerId == playerId)
                          && dbContext.Matches.Any(match => match.Id == matchEvent.MatchId && match.StageId == stageId),
            cancellationToken);
}
