using Stathub.Modules.Finance.Domain.Entities;
using Stathub.Modules.Finance.Domain.Enums;

namespace Stathub.Modules.Finance.Application.Interfaces;

public interface IFeeSettingRepository
{
    Task<FeeSetting?> GetAsync(
        FeeScopeType scopeType,
        Guid scopeId,
        CancellationToken cancellationToken = default);

    Task AddAsync(FeeSetting setting, CancellationToken cancellationToken = default);

    void Remove(FeeSetting setting);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IMatchChargeRepository
{
    Task<bool> AnyForMatchAsync(Guid matchId, CancellationToken cancellationToken = default);

    Task<MatchCharge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MatchCharge>> ListByStageAsync(
        Guid stageId,
        bool onlyDebt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MatchCharge>> ListByTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IReadOnlyCollection<MatchCharge> charges,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Имена команд для начислений (реализация — поверх модуля Teams).</summary>
public interface ITeamNameSource
{
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken = default);
}
