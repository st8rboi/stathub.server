using Stathub.Modules.Standings.Domain.Enums;

namespace Stathub.Modules.Standings.Application.Dtos;

/// <summary>Идущий матч команды глазами этой команды: её голы первыми.</summary>
/// <param name="Outcome">Текущий исход для команды — по нему витрина красит строку.</param>
public sealed record StandingsLiveMatchDto(
    Guid? MatchId,
    int GoalsFor,
    int GoalsAgainst,
    LiveOutcome Outcome);
