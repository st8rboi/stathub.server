namespace Stathub.WebApi.Contracts.Leagues;

/// <summary>
/// Запрос на создание нового турнира.
/// </summary>
/// <param name="Name"></param>
/// <param name="StartDate"></param>
/// <param name="EndDate"></param>
public sealed record CreateTournamentRequest(string Name, DateOnly StartDate, DateOnly? EndDate);
