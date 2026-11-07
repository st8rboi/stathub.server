using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Standings.Application.Interfaces;
using Stathub.Modules.Standings.Domain.Enums;
using Stathub.Modules.Standings.Domain.Models;

namespace Stathub.Modules.Standings.Infrastructure.Services;

/// <summary>
/// Адаптер «порт таблицы → читатель этапа модуля Leagues». Здесь же нейтральный
/// <see cref="TiebreakerKind"/> превращается в критерий домена расчёта таблицы.
/// </summary>
public sealed class StandingsRulesSource(IStagePublicReader stagePublicReader) : IStandingsRulesSource
{
    public async Task<StandingsRules?> GetAsync(Guid stageId, CancellationToken cancellationToken = default)
    {
        var stage = await stagePublicReader.GetAsync(stageId, cancellationToken);

        if (stage is null)
            return null;

        // «Points» — это основной ключ сортировки, а не тай-брейк, поэтому в список критериев не попадает.
        var tiebreakers = stage.Tiebreakers
            .Where(kind => kind != TiebreakerKind.Points)
            .Select(Map)
            .ToList();

        return new StandingsRules(
            stage.WinPoints,
            stage.DrawPoints,
            stage.LossPoints,
            tiebreakers);
    }

    private static StandingsTiebreaker Map(TiebreakerKind kind) => kind switch
    {
        TiebreakerKind.HeadToHead => StandingsTiebreaker.HeadToHead,
        TiebreakerKind.GoalDifference => StandingsTiebreaker.GoalDifference,
        TiebreakerKind.GoalsScored => StandingsTiebreaker.GoalsScored,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Неизвестный критерий тай-брейка.")
    };
}
