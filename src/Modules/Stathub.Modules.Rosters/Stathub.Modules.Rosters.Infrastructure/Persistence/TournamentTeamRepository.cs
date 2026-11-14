using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Rosters.Application.Interfaces;
using Stathub.Modules.Rosters.Domain.Entities;
using Stathub.Modules.Rosters.Domain.Enums;

namespace Stathub.Modules.Rosters.Infrastructure.Persistence;

internal sealed class TournamentTeamRepository(RostersDbContext dbContext) : ITournamentTeamRepository
{
    public Task<TournamentTeam?> GetByIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default) =>
        dbContext.TournamentTeams
            .Include(tournamentTeam => tournamentTeam.Registrations)
            .FirstOrDefaultAsync(tournamentTeam => tournamentTeam.Id == tournamentTeamId, cancellationToken);

    public Task<TournamentTeam?> GetByDivisionAndTeamAsync(
        Guid divisionId,
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        dbContext.TournamentTeams
            .Include(tournamentTeam => tournamentTeam.Registrations)
            .FirstOrDefaultAsync(
                tournamentTeam => tournamentTeam.DivisionId == divisionId && tournamentTeam.TeamId == teamId,
                cancellationToken);

    public Task<TournamentTeam?> GetByTournamentAndTeamAsync(
        Guid tournamentId,
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        dbContext.TournamentTeams
            .Include(tournamentTeam => tournamentTeam.Registrations)
            .FirstOrDefaultAsync(
                tournamentTeam => tournamentTeam.TournamentId == tournamentId && tournamentTeam.TeamId == teamId,
                cancellationToken);

    public async Task<IReadOnlyList<TournamentTeam>> ListByDivisionAsync(
        Guid divisionId,
        CancellationToken cancellationToken = default) =>
        await dbContext.TournamentTeams
            .AsNoTracking()
            .Include(tournamentTeam => tournamentTeam.Registrations)
            .Where(tournamentTeam => tournamentTeam.DivisionId == divisionId)
            .OrderBy(tournamentTeam => tournamentTeam.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TournamentTeam>> ListByTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        await dbContext.TournamentTeams
            .AsNoTracking()
            .Include(tournamentTeam => tournamentTeam.Registrations)
            .Where(tournamentTeam => tournamentTeam.TeamId == teamId)
            .OrderByDescending(tournamentTeam => tournamentTeam.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<Guid?> FindTeamIdWithActiveRegistrationAsync(
        Guid tournamentId,
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var result = await dbContext.TournamentTeams
            .AsNoTracking()
            // Граница уникальности — турнир: в одном турнире игрок играет за одну команду,
            // в каком бы дивизионе она ни была.
            .Where(tournamentTeam => tournamentTeam.TournamentId == tournamentId)
            .Where(tournamentTeam => tournamentTeam.Registrations.Any(registration =>
                registration.PlayerId == playerId &&
                registration.Status == PlayerRegistrationStatus.Active))
            .Select(tournamentTeam => (Guid?)tournamentTeam.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return result;
    }

    public async Task<IReadOnlyCollection<Guid>> FindPlayersWithActiveRegistrationAsync(
        Guid tournamentId,
        IReadOnlyCollection<Guid> playerIds,
        CancellationToken cancellationToken = default)
    {
        var ids = playerIds.ToList();

        var result = await dbContext.TournamentTeams
            .AsNoTracking()
            .Where(tournamentTeam => tournamentTeam.TournamentId == tournamentId)
            .SelectMany(tournamentTeam => tournamentTeam.Registrations)
            .Where(registration =>
                registration.Status == PlayerRegistrationStatus.Active && ids.Contains(registration.PlayerId))
            .Select(registration => registration.PlayerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return result.ToHashSet();
    }

    public async Task AddAsync(TournamentTeam tournamentTeam, CancellationToken cancellationToken = default) =>
        await dbContext.TournamentTeams.AddAsync(tournamentTeam, cancellationToken);

    public void Remove(TournamentTeam tournamentTeam) => dbContext.TournamentTeams.Remove(tournamentTeam);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
