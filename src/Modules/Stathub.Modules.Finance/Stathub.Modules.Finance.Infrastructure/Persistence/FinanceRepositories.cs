using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Finance.Application.Interfaces;
using Stathub.Modules.Finance.Domain.Entities;
using Stathub.Modules.Finance.Domain.Enums;
using Stathub.Modules.Teams.Application.Interfaces;

namespace Stathub.Modules.Finance.Infrastructure.Persistence;

internal sealed class FeeSettingRepository(FinanceDbContext dbContext) : IFeeSettingRepository
{
    public Task<FeeSetting?> GetAsync(
        FeeScopeType scopeType,
        Guid scopeId,
        CancellationToken cancellationToken = default) =>
        dbContext.FeeSettings.FirstOrDefaultAsync(
            setting => setting.ScopeType == scopeType && setting.ScopeId == scopeId,
            cancellationToken);

    public async Task AddAsync(FeeSetting setting, CancellationToken cancellationToken = default) =>
        await dbContext.FeeSettings.AddAsync(setting, cancellationToken);

    public void Remove(FeeSetting setting) => dbContext.FeeSettings.Remove(setting);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

internal sealed class MatchChargeRepository(FinanceDbContext dbContext) : IMatchChargeRepository
{
    public Task<bool> AnyForMatchAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        dbContext.MatchCharges.AnyAsync(charge => charge.MatchId == matchId, cancellationToken);

    public Task<MatchCharge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.MatchCharges.FirstOrDefaultAsync(charge => charge.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MatchCharge>> ListByMatchAsync(
        Guid matchId,
        CancellationToken cancellationToken = default) =>
        await dbContext.MatchCharges
            .Where(charge => charge.MatchId == matchId)
            .ToListAsync(cancellationToken);

    public void RemoveRange(IReadOnlyCollection<MatchCharge> charges) =>
        dbContext.MatchCharges.RemoveRange(charges);

    public async Task<IReadOnlyList<MatchCharge>> ListByStageAsync(
        Guid stageId,
        bool onlyDebt,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.MatchCharges
            .AsNoTracking()
            .Where(charge => charge.StageId == stageId);

        if (onlyDebt)
            query = query.Where(charge => charge.Status == ChargeStatus.Pending);

        return await query.OrderBy(charge => charge.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MatchCharge>> ListByTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default) =>
        await dbContext.MatchCharges
            .AsNoTracking()
            .Where(charge => charge.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

    public async Task AddRangeAsync(
        IReadOnlyCollection<MatchCharge> charges,
        CancellationToken cancellationToken = default) =>
        await dbContext.MatchCharges.AddRangeAsync(charges, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

internal sealed class TeamNameSource(ITeamReader teamReader) : ITeamNameSource
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken = default)
    {
        if (teamIds.Count == 0)
            return new Dictionary<Guid, string>();

        var teams = await teamReader.GetNamesAsync(teamIds, cancellationToken);
        return teams.ToDictionary(team => team.Id, team => team.Name);
    }
}
