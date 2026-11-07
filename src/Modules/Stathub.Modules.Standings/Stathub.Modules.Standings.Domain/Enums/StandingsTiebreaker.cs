namespace Stathub.Modules.Standings.Domain.Enums;

/// <summary>
/// Критерий тай-брейка в терминах модуля таблицы. Повторяет набор правил этапа,
/// но без ссылки на домен Leagues (межмодульные ссылки на домены запрещены).
/// </summary>
public enum StandingsTiebreaker
{
    HeadToHead = 0,
    GoalDifference = 1,
    GoalsScored = 2
}
