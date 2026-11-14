using Stathub.Modules.Rosters.Domain.Enums;

namespace Stathub.Modules.Rosters.Application.Dtos;

/// <summary>
/// Прошлая (или текущая) заявка команды в другом турнире, из которой можно скопировать состав.
/// </summary>
/// <param name="TournamentTeamId">Заявка-источник: её Id передаётся при заявке команды</param>
/// <param name="ActivePlayers">Сколько игроков будет скопировано (активные заявки источника)</param>
public sealed record RosterSourceDto(
    Guid TournamentTeamId,
    Guid TournamentId,
    string TournamentName,
    Guid DivisionId,
    string DivisionName,
    string LeagueName,
    TournamentTeamStatus Status,
    int ActivePlayers,
    DateTime CreatedAtUtc);

/// <summary>Итог заявки команды: сколько игроков скопировано и кто пропущен.</summary>
/// <param name="SkippedPlayerIds">
/// Игроки источника, уже заявленные за другую команду этого турнира — их переводят трансфером.
/// </param>
public sealed record AddTournamentTeamResult(
    Guid Id,
    int CopiedPlayers,
    IReadOnlyList<Guid> SkippedPlayerIds);
