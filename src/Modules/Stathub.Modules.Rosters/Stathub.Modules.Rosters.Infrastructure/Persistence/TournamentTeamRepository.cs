using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Rosters.Application.Interfaces;
using Stathub.Modules.Rosters.Domain.Entities;
using Stathub.Modules.Rosters.Domain.Enums;

namespace Stathub.Modules.Rosters.Infrastructure.Persistence;

internal sealed class TournamentTeamRepository(RostersDbContext dbContext) : ITournamentTeamRepository
{
    public Task<TournamentTeam?> GetByIdAsync(
        Guid tournamentTeamId,
        CancellationToken cancellationToken = default) =>
        dbContext.TournamentTeams
            .Include(tournamentTeam => tournamentTeam.Registrations)
            .FirstOrDefaultAsync(tournamentTeam => tournamentTeam.Id == tournamentTeamId, cancellationToken);

    public Task<TournamentTeam?> GetByTournamentAndTeamAsync(
        Guid tournamentId,
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        dbContext.TournamentTeams
            .Include(tournamentTeam => tournamentTeam.Registrations)
            .FirstOrDefaultAsync(
                tournamentTeam => tournamentTeam.TournamentId == tournamentId && tournamentTeam.TeamId == teamId,
                cancellationToken);

    public async Task<IReadOnlyList<TournamentTeam>> ListByTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default) =>
        await dbContext.TournamentTeams
            .AsNoTracking()
            .Include(tournamentTeam => tournamentTeam.Registrations)
            .Where(tournamentTeam => tournamentTeam.TournamentId == tournamentId)
            .OrderBy(tournamentTeam => tournamentTeam.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<Guid?> FindTeamIdWithActiveRegistrationAsync(
        Guid tournamentId,
        Guid playerId,
        CancellationToken cancellationToken = default) =>
        await dbContext.TournamentTeams
            .AsNoTracking()
            .Where(tournamentTeam => tournamentTeam.TournamentId == tournamentId)
            .SelectMany(tournamentTeam => tournamentTeam.Registrations)
            .Where(registration =>
                registration.PlayerId == playerId && registration.Status == PlayerRegistrationStatus.Active)
            .Select(registration => (Guid?)registration.TournamentTeamId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(TournamentTeam tournamentTeam, CancellationToken cancellationToken = default) =>
        await dbContext.TournamentTeams.AddAsync(tournamentTeam, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
