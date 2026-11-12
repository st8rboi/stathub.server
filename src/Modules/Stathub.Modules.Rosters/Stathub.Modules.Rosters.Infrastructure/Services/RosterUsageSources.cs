using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Players.Application.Dtos;
using Stathub.Modules.Players.Application.Interfaces;
using Stathub.Modules.Rosters.Infrastructure.Persistence;
using Stathub.Modules.Teams.Application.Dtos;
using Stathub.Modules.Teams.Application.Interfaces;

namespace Stathub.Modules.Rosters.Infrastructure.Services;

/// <summary>Заявки команды в дивизионы турниров — для проверки перед правкой и удалением команды.</summary>
internal sealed class RosterTeamUsageSource(
    RostersDbContext dbContext,
    ITournamentReader tournamentReader) : ITeamUsageSource
{
    public async Task<TeamUsageDto?> GetUsageAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var tournamentIds = await dbContext.TournamentTeams
            .AsNoTracking()
            .Where(tournamentTeam => tournamentTeam.TeamId == teamId)
            .Select(tournamentTeam => tournamentTeam.TournamentId)
            .ToListAsync(cancellationToken);

        if (tournamentIds.Count == 0)
            return null;

        var organizerIds = await tournamentReader.GetOrganizerIdsAsync(tournamentIds, cancellationToken);

        return new TeamUsageDto($"заявки: {tournamentIds.Count}", organizerIds);
    }
}

/// <summary>Регистрации игрока в составах — для проверки перед правкой и удалением игрока.</summary>
internal sealed class RosterPlayerUsageSource(
    RostersDbContext dbContext,
    ITournamentReader tournamentReader) : IPlayerUsageSource
{
    public async Task<PlayerUsageDto?> GetUsageAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        // Отчисленные регистрации тоже считаются: они остаются в истории состава.
        var tournamentIds = await dbContext.PlayerRegistrations
            .AsNoTracking()
            .Where(registration => registration.PlayerId == playerId)
            .Join(
                dbContext.TournamentTeams,
                registration => registration.TournamentTeamId,
                tournamentTeam => tournamentTeam.Id,
                (_, tournamentTeam) => tournamentTeam.TournamentId)
            .ToListAsync(cancellationToken);

        if (tournamentIds.Count == 0)
            return null;

        var organizerIds = await tournamentReader.GetOrganizerIdsAsync(tournamentIds, cancellationToken);

        return new PlayerUsageDto($"заявки: {tournamentIds.Count}", organizerIds);
    }
}

/// <summary>Заявки команд на турнир и дивизион — для проверки перед удалением турнира или дивизиона.</summary>
internal sealed class RosterCompetitionUsageSource(RostersDbContext dbContext) : ICompetitionUsageSource
{
    public async Task<string?> GetTournamentUsageAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var count = await dbContext.TournamentTeams
            .CountAsync(tournamentTeam => tournamentTeam.TournamentId == tournamentId, cancellationToken);

        return count > 0 ? $"заявки команд: {count}" : null;
    }

    public async Task<string?> GetDivisionUsageAsync(Guid divisionId, CancellationToken cancellationToken = default)
    {
        var count = await dbContext.TournamentTeams
            .CountAsync(tournamentTeam => tournamentTeam.DivisionId == divisionId, cancellationToken);

        return count > 0 ? $"заявки команд: {count}" : null;
    }

    // Заявки привязаны к дивизиону, а не к этапу: удалению этапа они не мешают.
    public Task<string?> GetStageUsageAsync(Guid stageId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    // Заявки не мешают менять правила этапа: формат матча и очки на них не влияют.
    public Task<bool> StageHasMatchesAsync(Guid stageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

/// <summary>Команды дивизиона для модуля Leagues: в группу этапа попадают только заявленные команды.</summary>
internal sealed class RosterDivisionTeamSource(RostersDbContext dbContext) : IDivisionTeamSource
{
    public async Task<IReadOnlyCollection<Guid>> GetTeamIdsAsync(
        Guid divisionId,
        CancellationToken cancellationToken = default) =>
        await dbContext.TournamentTeams
            .AsNoTracking()
            .Where(tournamentTeam => tournamentTeam.DivisionId == divisionId)
            .Select(tournamentTeam => tournamentTeam.TeamId)
            .ToListAsync(cancellationToken);
}
