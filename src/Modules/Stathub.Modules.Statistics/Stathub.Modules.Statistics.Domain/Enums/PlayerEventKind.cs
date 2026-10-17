namespace Stathub.Modules.Statistics.Domain.Enums;

/// <summary>
/// Событие, которое попадает в личную статистику игрока.
/// <see cref="Assist"/> не существует отдельным событием протокола: он выводится
/// из <c>RelatedPlayerId</c> гола.
/// </summary>
public enum PlayerEventKind
{
    Goal = 0,
    OwnGoal = 1,
    YellowCard = 2,
    RedCard = 3,
    Assist = 4
}
