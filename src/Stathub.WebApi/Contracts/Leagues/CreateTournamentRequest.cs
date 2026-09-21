namespace Stathub.WebApi.Contracts.Leagues;

public sealed record CreateTournamentRequest(string Name, DateOnly StartDate, DateOnly? EndDate);
