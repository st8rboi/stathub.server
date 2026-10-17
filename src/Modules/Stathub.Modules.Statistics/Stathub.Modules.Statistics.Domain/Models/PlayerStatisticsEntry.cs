namespace Stathub.Modules.Statistics.Domain.Models;

/// <summary>
/// Строка статистики игрока за турнир.
/// <para>
/// <see cref="Matches"/> — число матчей, в которых у игрока было хотя бы одно событие.
/// Составов в модели пока нет, поэтому «матчи» — это именно матчи с событиями, а не выходы на поле.
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
    int Matches);
