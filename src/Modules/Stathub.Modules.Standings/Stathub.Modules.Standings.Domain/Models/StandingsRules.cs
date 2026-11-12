using Stathub.Modules.Standings.Domain.Enums;

namespace Stathub.Modules.Standings.Domain.Models;

/// <summary>
/// Правила подсчёта: очки за победу/ничью/поражение (PointsRule этапа), порядок тай-брейков,
/// а также разбиение этапа на группы и число команд, выходящих дальше.
/// </summary>
/// <param name="QualifiersCount">Сколько первых команд выходит дальше (при группах — из каждой); null — никто.</param>
/// <param name="Groups">Группы этапа; пусто или null — одна общая таблица.</param>
public sealed record StandingsRules(
    int WinPoints,
    int DrawPoints,
    int LossPoints,
    IReadOnlyList<StandingsTiebreaker> Tiebreakers,
    int? QualifiersCount = null,
    IReadOnlyList<StandingsGroup>? Groups = null)
{
    public static StandingsRules Default { get; } = new(
        WinPoints: 3,
        DrawPoints: 1,
        LossPoints: 0,
        Tiebreakers: [StandingsTiebreaker.GoalDifference, StandingsTiebreaker.GoalsScored]);
}

/// <summary>Группа этапа: её команды попадают в таблицу ещё до первого матча.</summary>
public sealed record StandingsGroup(Guid Id, string Name, int Order, IReadOnlyList<Guid> TeamIds);
