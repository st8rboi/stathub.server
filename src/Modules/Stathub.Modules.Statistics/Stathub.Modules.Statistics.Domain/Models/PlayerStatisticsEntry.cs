namespace Stathub.Modules.Statistics.Domain.Models;

/// <summary>
/// Строка статистики игрока за турнир.
/// <para>
/// <see cref="Matches"/> — число матчей, в которых у игрока было хотя бы одно событие.
/// Составов в модели пока нет, поэтому «матчи» — это именно матчи с событиями, а не выходы на поле.
/// </para>
/// <para>
/// <see cref="Goals"/> и <see cref="Assists"/> учитывают вес бонусного мяча (за два).
/// <see cref="PenaltyGoals"/> — сколько из голов забито с пенальти, <see cref="PenaltiesMissed"/> — сколько не забито.
/// </para>
/// </summary>
public sealed record PlayerStatisticsEntry(
    Guid PlayerId,
    Guid? TeamId,
    int Goals,
    int OwnGoals,
    int Assists,
    int YellowCards,
    int RedCards,
    int Matches,
    int PenaltyGoals = 0,
    int PenaltiesMissed = 0);
