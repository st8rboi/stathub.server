namespace Stathub.Modules.Standings.Domain.Models;

/// <summary>Результат завершённого матча в терминах расчёта таблицы.</summary>
/// <param name="GroupId">Группа этапа, в которой сыгран матч; null — этап без групп.</param>
public sealed record StandingsMatch(
    Guid HomeTeamId,
    Guid AwayTeamId,
    int HomeScore,
    int AwayScore,
    Guid? GroupId = null);
