using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.Enums;

namespace Stathub.Modules.Matches.Application.Interfaces;

public interface IMatchRepository
{
    Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Match>> GetByStageIdAsync(Guid stageId, CancellationToken cancellationToken = default);

    /// <summary>Матчи в заданных статусах — публичная live-лента «сейчас играют».</summary>
    Task<IReadOnlyList<Match>> GetByStatusAsync(
        IReadOnlyCollection<MatchStatus> statuses,
        CancellationToken cancellationToken = default);

    /// <summary>Матчи с запланированным началом в интервале [fromUtc, toUtc) — расписание по дате.</summary>
    Task<IReadOnlyList<Match>> GetByPeriodAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task AddAsync(Match match, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

