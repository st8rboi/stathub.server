namespace Stathub.WebApi.Contracts.Rosters;

/// <summary>Заявка игрока за команду турнира.</summary>
public sealed record RegisterPlayerRequest(Guid PlayerId);
