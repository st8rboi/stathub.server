namespace Stathub.Modules.Standings.Domain.Models;

/// <summary>
/// Таблица этапа или одной его группы. Строки уже отсортированы; первые
/// <see cref="QualifiersCount"/> из них выходят в следующий этап.
/// </summary>
/// <param name="GroupId">Группа; null — общая таблица этапа без групп.</param>
public sealed record StandingsTable(
    Guid? GroupId,
    string? GroupName,
    IReadOnlyList<StandingsEntry> Entries,
    int QualifiersCount)
{
    /// <summary>Выходит ли дальше команда на указанном месте (с единицы).</summary>
    public bool Qualifies(int position) => position <= QualifiersCount;
}
