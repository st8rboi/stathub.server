namespace Stathub.Modules.Standings.Domain.Models;

/// <summary>
/// Таблица этапа или одной его группы. Строки уже отсортированы; первые
/// <see cref="QualifiersCount"/> из них выходят в следующий этап, последние
/// <see cref="EliminatedCount"/> — вылетают.
/// </summary>
/// <param name="GroupId">Группа; null — общая таблица этапа без групп.</param>
public sealed record StandingsTable(
    Guid? GroupId,
    string? GroupName,
    IReadOnlyList<StandingsEntry> Entries,
    int QualifiersCount,
    int EliminatedCount = 0)
{
    /// <summary>Выходит ли дальше команда на указанном месте (с единицы).</summary>
    public bool Qualifies(int position) => position <= QualifiersCount;

    /// <summary>
    /// Вылетает ли команда на указанном месте (с единицы). Если зоны пересекаются (команд меньше,
    /// чем выходит и вылетает вместе), место считается выходящим дальше.
    /// </summary>
    public bool Eliminated(int position) =>
        !Qualifies(position) && position > Entries.Count - EliminatedCount;
}
