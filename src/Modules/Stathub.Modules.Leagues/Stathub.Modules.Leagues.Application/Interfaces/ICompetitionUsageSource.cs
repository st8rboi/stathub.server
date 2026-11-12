namespace Stathub.Modules.Leagues.Application.Interfaces;

/// <summary>
/// Где используются турнир и этап. Объявлен здесь, а реализуется модулями-потребителями
/// (заявки, матчи): Leagues не знает про них, но перед удалением или сменой правил спрашивает
/// все зарегистрированные источники. Между таблицами модулей нет FK, поэтому это единственная
/// защита от «висячих» ссылок.
/// </summary>
public interface ICompetitionUsageSource
{
    /// <summary>Описание ссылок на турнир (например, «матчи: 12») или null, если их нет.</summary>
    Task<string?> GetTournamentUsageAsync(Guid tournamentId, CancellationToken cancellationToken = default);

    /// <summary>Описание ссылок на дивизион (заявки команд) или null, если их нет.</summary>
    Task<string?> GetDivisionUsageAsync(Guid divisionId, CancellationToken cancellationToken = default);

    /// <summary>Описание ссылок на этап или null, если их нет.</summary>
    Task<string?> GetStageUsageAsync(Guid stageId, CancellationToken cancellationToken = default);

    /// <summary>Есть ли в этапе матчи — от этого зависит, можно ли менять его правила.</summary>
    Task<bool> StageHasMatchesAsync(Guid stageId, CancellationToken cancellationToken = default);
}
