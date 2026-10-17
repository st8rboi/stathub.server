namespace Stathub.Modules.Standings.Domain.Models;

/// <summary>Строка турнирной таблицы — результат расчёта, нигде не сохраняется (§9.2).</summary>
public sealed record StandingsEntry(
    Guid TeamId,
    int Played,
    int Wins,
    int Draws,
    int Losses,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDifference,
    int Points);
