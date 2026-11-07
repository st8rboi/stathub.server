namespace Stathub.Modules.Standings.Application.Dtos;

/// <summary>Таблица этапа. Считается на каждый GET и не кэшируется (documentation/doc.md §9.2).</summary>
public sealed record StageStandingsDto(
    Guid StageId,
    IReadOnlyList<StandingsRowDto> Rows);
