using Stathub.Modules.Standings.Domain.Enums;

namespace Stathub.Modules.Standings.Domain.Models;

/// <summary>
/// Правила подсчёта: очки за победу/ничью/поражение (PointsRule этапа) и порядок тай-брейков.
/// </summary>
public sealed record StandingsRules(
    int WinPoints,
    int DrawPoints,
    int LossPoints,
    IReadOnlyList<StandingsTiebreaker> Tiebreakers)
{
    public static StandingsRules Default { get; } = new(
        WinPoints: 3,
        DrawPoints: 1,
        LossPoints: 0,
        Tiebreakers: [StandingsTiebreaker.GoalDifference, StandingsTiebreaker.GoalsScored]);
}
