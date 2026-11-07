namespace Stathub.WebApi.Contracts.Leagues;

/// <summary>Изменение названия и дат турнира.</summary>
public sealed record UpdateTournamentRequest(string Name, DateOnly StartDate, DateOnly? EndDate);
