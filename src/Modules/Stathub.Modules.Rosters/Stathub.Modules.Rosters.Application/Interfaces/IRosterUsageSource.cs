namespace Stathub.Modules.Rosters.Application.Interfaces;

/// <summary>
/// Использование заявок в матчах. Объявлен здесь, а реализуется модулем матчей: Rosters
/// не знает про Matches, но перед удалением заявки спрашивает его. Между таблицами модулей
/// нет FK, поэтому это единственная защита от «висячих» ссылок.
/// </summary>
public interface IRosterUsageSource
{
    /// <summary>Сколько матчей у команды в дивизионе (любого статуса, включая запланированные).</summary>
    Task<int> CountTeamMatchesAsync(Guid divisionId, Guid teamId, CancellationToken cancellationToken = default);

    /// <summary>Есть ли у игрока события в матчах дивизиона за эту команду (включая отменённые).</summary>
    Task<bool> PlayerHasEventsAsync(
        Guid divisionId,
        Guid teamId,
        Guid playerId,
        CancellationToken cancellationToken = default);
}
