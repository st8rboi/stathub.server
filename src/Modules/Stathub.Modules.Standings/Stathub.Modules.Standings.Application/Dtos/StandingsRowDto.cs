namespace Stathub.Modules.Standings.Application.Dtos;

/// <param name="Position">Место в своей таблице (в группе или в общей таблице этапа), с единицы.</param>
/// <param name="Qualifies">Команда в зоне выхода в следующий этап.</param>
public sealed record StandingsRowDto(
    Guid TeamId,
    string TeamName,
    string? TeamLogoUrl,
    int Played,
    int Wins,
    int Draws,
    int Losses,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDifference,
    int Points,
    int Position,
    bool Qualifies);
