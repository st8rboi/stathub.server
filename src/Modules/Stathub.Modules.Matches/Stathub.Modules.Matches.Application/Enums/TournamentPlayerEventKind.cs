namespace Stathub.Modules.Matches.Application.Enums;

/// <summary>
/// Нейтральный вид события для статистики игрока. Доменный <c>MatchEventType</c> шире
/// (замена, отмена события) и не должен утекать в другие модули, поэтому наружу отдаём
/// только то, что участвует в подсчёте.
/// </summary>
public enum TournamentPlayerEventKind
{
    Goal = 0,
    OwnGoal = 1,
    YellowCard = 2,
    RedCard = 3
}
