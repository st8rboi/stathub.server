using Stathub.Modules.Standings.Domain.Models;

namespace Stathub.Modules.Standings.Application.Interfaces;

/// <summary>Правила подсчёта этапа: очки и порядок тай-брейков (реализация — поверх Leagues).</summary>
public interface IStandingsRulesSource
{
    Task<StandingsRules?> GetAsync(Guid stageId, CancellationToken cancellationToken = default);
}
