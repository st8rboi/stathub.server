namespace Stathub.Modules.Rosters.Application.Interfaces;

/// <summary>
/// Порт потребителя: сведения о дивизионе (и его турнире) глазами заявок.
/// Не содержит доменных типов Leagues, поэтому Application модуля Rosters остаётся
/// зависимым только от своего домена и Shared. Реализация — в Rosters.Infrastructure.
/// </summary>
public interface IDivisionInfoProvider
{
    Task<DivisionRosterInfo?> GetAsync(Guid divisionId, CancellationToken cancellationToken = default);

    /// <summary>Дивизион, в котором проходит этап: состав матча ищут по этапу матча.</summary>
    Task<DivisionRosterInfo?> GetByStageAsync(Guid stageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Названия и владельцы дивизионов — для списка заявок-источников при копировании состава.
    /// Неизвестные (удалённые) дивизионы в ответ не попадают.
    /// </summary>
    Task<IReadOnlyList<DivisionSourceInfo>> GetSourceInfosAsync(
        IReadOnlyCollection<Guid> divisionIds,
        CancellationToken cancellationToken = default);
}

/// <param name="DivisionId">Дивизион, в который заявляется команда</param>
/// <param name="TournamentId">Турнир дивизиона: граница уникальности игрока и блокировки заявок</param>
/// <param name="RosterLocked">Турнир завершён или отменён — заявки менять нельзя</param>
public sealed record DivisionRosterInfo(Guid DivisionId, Guid TournamentId, bool RosterLocked);

/// <param name="OrganizerId">Владелец лиги: свои заявки организатор видит всегда</param>
/// <param name="IsPublished">Лига опубликована — её заявки и так видны на витрине</param>
public sealed record DivisionSourceInfo(
    Guid DivisionId,
    string DivisionName,
    Guid TournamentId,
    string TournamentName,
    string LeagueName,
    Guid OrganizerId,
    bool IsPublished);
