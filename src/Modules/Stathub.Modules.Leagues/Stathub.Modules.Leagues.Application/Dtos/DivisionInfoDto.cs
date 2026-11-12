namespace Stathub.Modules.Leagues.Application.Dtos;

/// <summary>
/// Минимум сведений о дивизионе для других модулей (заявки, статистика) — без доменных типов Leagues.
/// </summary>
/// <param name="DivisionsCount">Сколько дивизионов в турнире: при одном уровень дивизиона скрывается.</param>
/// <param name="StageIds">Этапы дивизиона в порядке проведения.</param>
public sealed record DivisionInfoDto(
    Guid DivisionId,
    string Name,
    int Order,
    Guid TournamentId,
    int DivisionsCount,
    IReadOnlyList<Guid> StageIds);
