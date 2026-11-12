namespace Stathub.Modules.Standings.Application.Dtos;

/// <summary>
/// Таблица этапа. Считается на каждый GET и не кэшируется (documentation/doc.md §9.2).
/// </summary>
/// <param name="Rows">Все строки этапа; при группах — подряд по группам.</param>
/// <param name="Groups">Таблицы групп; пусто — этап без групп, тогда вся таблица в <paramref name="Rows"/>.</param>
/// <param name="QualifiersCount">Сколько первых команд выходит дальше (при группах — из каждой); null — никто.</param>
public sealed record StageStandingsDto(
    Guid StageId,
    IReadOnlyList<StandingsRowDto> Rows,
    IReadOnlyList<StandingsGroupDto> Groups,
    int? QualifiersCount);

public sealed record StandingsGroupDto(
    Guid GroupId,
    string Name,
    IReadOnlyList<StandingsRowDto> Rows);
