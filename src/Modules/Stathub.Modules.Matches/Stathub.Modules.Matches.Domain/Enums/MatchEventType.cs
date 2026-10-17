namespace Stathub.Modules.Matches.Domain.Enums;

/// <summary>
/// Тип события протокола матча.
/// Отмена события — отдельный универсальный тип <see cref="EventVoided"/> со ссылкой
/// на отменяемое событие, а не отдельные типы «отменённый гол» / «отменённая карточка»
/// (см. AGENTS.md, «Особенности домена»).
/// </summary>
public enum MatchEventType
{
    Goal = 0, // Гол
    OwnGoal = 1, // Автогол
    YellowCard = 2, // Жёлтая карточка
    RedCard = 3, // Красная карточка
    Substitution = 4, // Замена
    EventVoided = 5 // Отмена другого события протокола
}
