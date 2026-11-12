using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Standings.Application.Interfaces;
using Stathub.Modules.Standings.Domain.Enums;
using Stathub.Modules.Standings.Domain.Models;

namespace Stathub.Modules.Standings.Infrastructure.Services;

/// <summary>
/// Адаптер «порт таблицы → читатель этапа модуля Leagues». Здесь же нейтральный
/// <see cref="TiebreakerKind"/> превращается в критерий домена расчёта таблицы.
/// Участники общей таблицы кругового этапа — команды, заявленные в дивизион (порт реализует модуль заявок).
/// </summary>
public sealed class StandingsRulesSource(
    IStagePublicReader stagePublicReader,
    IEnumerable<IDivisionTeamSource> divisionTeamSources) : IStandingsRulesSource
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

        var groups = stage.Groups
            .Select(group => new StandingsGroup(group.GroupId, group.Name, group.Order, group.TeamIds))
            .ToList();

        // Без групп общая таблица кругового этапа сразу показывает всех заявленных участников с нулями.
        // В плей-офф таблицы по смыслу нет, поэтому туда весь дивизион не подмешиваем.
        var teamIds = new HashSet<Guid>();
        if (groups.Count == 0 && stage.FormatType == RoundFormat)
        {
            foreach (var source in divisionTeamSources)
                teamIds.UnionWith(await source.GetTeamIdsAsync(stage.DivisionId, cancellationToken));
        }

        return new StandingsRules(
            stage.WinPoints,
            stage.DrawPoints,
            stage.LossPoints,
            tiebreakers,
            stage.QualifiersCount,
            groups,
            teamIds.ToList());
    }

    /// <summary>Строковое имя кругового формата в <see cref="StagePublicInfoDto.FormatType"/>.</summary>
    private const string RoundFormat = "Round";

    private static StandingsTiebreaker Map(TiebreakerKind kind) => kind switch
    {
        TiebreakerKind.HeadToHead => StandingsTiebreaker.HeadToHead,
        TiebreakerKind.GoalDifference => StandingsTiebreaker.GoalDifference,
        TiebreakerKind.GoalsScored => StandingsTiebreaker.GoalsScored,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Неизвестный критерий тай-брейка.")
    };
}
