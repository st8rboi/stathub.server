using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Rosters.Application.Interfaces;
using Stathub.Modules.Rosters.Domain.Entities;

namespace Stathub.Modules.Rosters.Infrastructure.Persistence;

internal sealed class TournamentTeamRepository(RostersDbContext dbContext) : ITournamentTeamRepository
{
    public Task<TournamentTeam?> GetByIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default) =>
        dbContext.TournamentTeams
            .Include(tournamentTeam => tournamentTeam.Registrations)
            .FirstOrDefaultAsync(tournamentTeam => tournamentTeam.Id == tournamentTeamId, cancellationToken);

    public Task<TournamentTeam?> GetByStageAndTeamAsync(
        Guid stageId,
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        dbContext.TournamentTeams
            .Include(tournamentTeam => tournamentTeam.Registrations)
            .FirstOrDefaultAsync(
                tournamentTeam => tournamentTeam.StageId == stageId && tournamentTeam.TeamId == teamId,
                cancellationToken);

    public async Task<IReadOnlyList<TournamentTeam>> ListByStageAsync(
        Guid stageId,
        CancellationToken cancellationToken = default) =>
        await dbContext.TournamentTeams
            .AsNoTracking()
            .Include(tournamentTeam => tournamentTeam.Registrations)
            .Where(tournamentTeam => tournamentTeam.StageId == stageId)
            .OrderBy(tournamentTeam => tournamentTeam.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<Guid?> FindTeamIdWithActiveRegistrationAsync(
        Guid stageId,
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var result = await dbContext.TournamentTeams
            .AsNoTracking()
            // Границей уникальности выбран этап: в разных дивизионах турнира
            // один и тот же игрок может быть заявлен за разные команды.
            .Where(tournamentTeam => tournamentTeam.StageId == stageId)
            .Where(tournamentTeam => tournamentTeam.Registrations.Any(registration =>
                registration.PlayerId == playerId &&
                registration.Status == Domain.Enums.PlayerRegistrationStatus.Active))
            .Select(tournamentTeam => (Guid?)tournamentTeam.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return result;
    }

    public async Task AddAsync(TournamentTeam tournamentTeam, CancellationToken cancellationToken = default) =>
        await dbContext.TournamentTeams.AddAsync(tournamentTeam, cancellationToken);

    public void Remove(TournamentTeam tournamentTeam) => dbContext.TournamentTeams.Remove(tournamentTeam);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
