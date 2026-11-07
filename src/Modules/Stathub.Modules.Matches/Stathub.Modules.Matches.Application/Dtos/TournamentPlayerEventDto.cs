using Stathub.Modules.Matches.Application.Enums;

namespace Stathub.Modules.Matches.Application.Dtos;

/// <summary>
/// Событие турнира, значимое для статистики игрока. Отменённые события сюда не попадают:
/// отмена — часть лога, а не состояние, и разбирается на стороне Matches.
/// </summary>
public sealed record TournamentPlayerEventDto(
    Guid MatchId,
    Guid? PlayerId,
    Guid? TeamId,
    Guid? RelatedPlayerId,
    TournamentPlayerEventKind Kind);
