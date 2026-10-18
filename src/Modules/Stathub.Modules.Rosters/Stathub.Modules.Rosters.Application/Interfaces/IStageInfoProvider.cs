namespace Stathub.Modules.Rosters.Application.Interfaces;

/// <summary>
/// Порт потребителя: сведения об этапе (и его турнире) глазами заявок.
/// Не содержит доменных типов Leagues, поэтому Application модуля Rosters остаётся
/// зависимым только от своего домена и Shared. Реализация — в Rosters.Infrastructure.
/// </summary>
public interface IStageInfoProvider
{
    Task<StageRosterInfo?> GetAsync(Guid stageId, CancellationToken cancellationToken = default);
}

/// <param name="StageId">Этап (группа/дивизион/плей-офф), на который заявляется команда</param>
/// <param name="TournamentId">Турнир этапа: контекст для проверок и лимитов</param>
/// <param name="RosterLocked">Турнир завершён или отменён — заявки менять нельзя</param>
public sealed record StageRosterInfo(Guid StageId, Guid TournamentId, bool RosterLocked);
