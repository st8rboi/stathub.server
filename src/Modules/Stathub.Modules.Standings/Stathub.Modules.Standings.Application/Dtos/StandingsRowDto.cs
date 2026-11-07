namespace Stathub.Modules.Standings.Application.Dtos;

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
    int Points);
