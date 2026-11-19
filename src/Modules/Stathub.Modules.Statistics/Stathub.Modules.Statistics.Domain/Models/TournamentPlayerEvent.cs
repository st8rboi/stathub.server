using Stathub.Modules.Statistics.Domain.Enums;

namespace Stathub.Modules.Statistics.Domain.Models;

/// <summary>
/// Событие игрока, уже очищенное от отмен и служебных типов.
/// <paramref name="RelatedPlayerId"/> у гола — ассистент (второй игрок события).
/// <paramref name="Value"/> — вес гола: бонусный мяч засчитывается автору и ассистенту за два.
/// <paramref name="IsPenalty"/> — гол забит с пенальти.
/// </summary>
public sealed record TournamentPlayerEvent(
    Guid MatchId,
    Guid? PlayerId,
    Guid? TeamId,
    Guid? RelatedPlayerId,
    PlayerEventKind Kind,
    int Value = 1,
    bool IsPenalty = false);
