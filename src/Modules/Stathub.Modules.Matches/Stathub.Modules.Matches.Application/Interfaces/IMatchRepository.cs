using Stathub.Modules.Matches.Application.Dtos;
using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.Enums;

namespace Stathub.Modules.Matches.Application.Interfaces;

public interface IMatchRepository
{
    Task<Match?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Match>> GetByStageIdAsync(Guid stageId, CancellationToken cancellationToken = default);

    /// <summary>Матчи по набору идентификаторов — «мои матчи» судьи по назначениям.</summary>
    Task<IReadOnlyList<Match>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>Матчи команды (дома и в гостях) во всех этапах — публичная страница команды.</summary>
    Task<IReadOnlyList<Match>> GetByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default);

    /// <summary>Матчи в заданных статусах — публичная live-лента «сейчас играют».</summary>
    Task<IReadOnlyList<Match>> GetByStatusAsync(
        IReadOnlyCollection<MatchStatus> statuses,
        CancellationToken cancellationToken = default);

    /// <summary>Матчи с запланированным началом в интервале [fromUtc, toUtc) — расписание по дате.</summary>
    Task<IReadOnlyList<Match>> GetByPeriodAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Этап и время начала всех матчей — без загрузки самих матчей (полоса дат витрины).</summary>
    Task<IReadOnlyList<MatchSlotDto>> GetSlotsAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Match match, CancellationToken cancellationToken = default);

    void Remove(Match match);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

