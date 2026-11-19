using Stathub.Modules.Matches.Application.Enums;

namespace Stathub.Modules.Matches.Application.Dtos;

/// <summary>
/// Событие турнира, значимое для статистики игрока. Отменённые события сюда не попадают:
/// отмена — часть лога, а не состояние, и разбирается на стороне Matches.
/// <para>
/// <see cref="Value"/> — вес гола: бонусный мяч приносит автору и ассистенту по два.
/// <see cref="IsPenalty"/> — гол забит с пенальти. Сам вид гола (доменный enum) наружу не отдаём.
/// </para>
/// </summary>
public sealed record TournamentPlayerEventDto(
    Guid MatchId,
    Guid? PlayerId,
    Guid? TeamId,
    Guid? RelatedPlayerId,
    TournamentPlayerEventKind Kind,
    int Value = 1,
    bool IsPenalty = false);
