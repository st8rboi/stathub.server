namespace Stathub.WebApi.Contracts.Rosters;

/// <summary>Заявка команды на турнир (сезон).</summary>
/// <param name="CopyFromTournamentTeamId">
/// Необязательно: заявка этой же команды в другом турнире, из которой скопировать состав
/// (список — <c>GET /divisions/{divisionId}/roster-sources?teamId=</c>).
/// </param>
public sealed record AddTournamentTeamRequest(Guid TeamId, Guid? CopyFromTournamentTeamId = null);
