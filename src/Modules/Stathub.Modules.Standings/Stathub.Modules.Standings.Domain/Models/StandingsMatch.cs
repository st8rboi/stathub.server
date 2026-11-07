namespace Stathub.Modules.Standings.Domain.Models;

/// <summary>Результат завершённого матча в терминах расчёта таблицы.</summary>
public sealed record StandingsMatch(
    Guid HomeTeamId,
    Guid AwayTeamId,
    int HomeScore,
    int AwayScore);
