using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Shared.Domain;

namespace Stathub.WebApi.Contracts.Matches;

/// <summary>
/// Запрос на создание нового матча.
/// </summary>
/// <param name="StageId"></param>
/// <param name="HomeTeamId"></param>
/// <param name="AwayTeamId"></param>
/// <param name="StartTimeUtc"></param>

public sealed record CreateMatchRequest(
    Guid StageId,
    Guid HomeTeamId,
    Guid AwayTeamId,
    DateTime StartTimeUtc);
