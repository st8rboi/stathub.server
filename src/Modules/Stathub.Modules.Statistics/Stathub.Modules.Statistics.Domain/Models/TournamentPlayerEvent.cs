using Stathub.Modules.Statistics.Domain.Enums;

namespace Stathub.Modules.Statistics.Domain.Models;

/// <summary>
/// Событие игрока, уже очищенное от отмен и служебных типов.
/// <paramref name="RelatedPlayerId"/> у гола — ассистент (второй игрок события).
/// </summary>
public sealed record TournamentPlayerEvent(
    Guid MatchId,
    Guid? PlayerId,
    Guid? TeamId,
    Guid? RelatedPlayerId,
    PlayerEventKind Kind);
