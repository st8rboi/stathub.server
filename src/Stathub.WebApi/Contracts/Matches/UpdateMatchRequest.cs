namespace Stathub.WebApi.Contracts.Matches;

/// <summary>
/// Перенос матча и замена команд. Только у не начатого матча без событий;
/// обе команды должны быть заявлены на этап матча.
/// </summary>
public sealed record UpdateMatchRequest(Guid HomeTeamId, Guid AwayTeamId, DateTime StartTimeUtc);
