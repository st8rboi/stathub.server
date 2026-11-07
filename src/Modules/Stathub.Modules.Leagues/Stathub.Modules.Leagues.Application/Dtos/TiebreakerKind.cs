namespace Stathub.Modules.Leagues.Application.Dtos;

/// <summary>
/// Критерий тай-брейка в терминах контракта модуля, а не доменного enum'а: потребители
/// (например, модуль расчёта таблицы) не должны ссылаться на домен Leagues.
/// </summary>
public enum TiebreakerKind
{
    Points = 0,
    HeadToHead = 1,
    GoalDifference = 2,
    GoalsScored = 3
}
