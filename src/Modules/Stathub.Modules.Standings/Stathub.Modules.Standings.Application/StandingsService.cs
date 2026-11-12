using Stathub.Modules.Standings.Application.Dtos;
using Stathub.Modules.Standings.Application.Interfaces;
using Stathub.Modules.Standings.Domain;
using Stathub.Modules.Standings.Domain.Models;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Standings.Application;

/// <summary>
/// Таблица этапа: правила и результаты читаются портами, расчёт — в домене.
/// Кэша нет по замыслу: при исправлении события инвалидировать нечего (§9.2, §9.1).
/// </summary>
public sealed class StandingsService(
    IStandingsRulesSource rulesSource,
    IStandingsMatchSource matchSource,
    IStandingsTeamNameSource teamNameSource)
{
    public async Task<StageStandingsDto> GetStageStandingsAsync(
        Guid stageId,
        CancellationToken cancellationToken = default)
    {
        var rules = await rulesSource.GetAsync(stageId, cancellationToken)
            ?? throw new NotFoundException($"Правила этапа '{stageId}' не найдены.");

        var matches = await matchSource.GetFinishedMatchesAsync(stageId, cancellationToken);
        var tables = StandingsCalculator.CalculateTables(matches, rules);

        var teamIds = tables
            .SelectMany(table => table.Entries)
            .Select(entry => entry.TeamId)
            .Distinct()
            .ToList();
        var teams = teamIds.Count == 0
            ? new Dictionary<Guid, StandingsTeamDto>()
            : await teamNameSource.GetTeamsAsync(teamIds, cancellationToken);

        var tableRows = tables
            .Select(table => (table, rows: ToRows(table, teams)))
            .ToList();

        return new StageStandingsDto(
            stageId,
            tableRows.SelectMany(item => item.rows).ToList(),
            tableRows
                .Where(item => item.table.GroupId is not null)
                .Select(item => new StandingsGroupDto(
                    item.table.GroupId!.Value,
                    item.table.GroupName ?? string.Empty,
                    item.rows))
                .ToList(),
            rules.QualifiersCount);
    }

    private static List<StandingsRowDto> ToRows(
        StandingsTable table,
        IReadOnlyDictionary<Guid, StandingsTeamDto> teams) =>
        table.Entries
            .Select((entry, index) =>
            {
                var team = teams.GetValueOrDefault(entry.TeamId);
                var position = index + 1;

                return new StandingsRowDto(
                    entry.TeamId,
                    team?.Name ?? string.Empty,
                    team?.LogoUrl,
                    entry.Played,
                    entry.Wins,
                    entry.Draws,
                    entry.Losses,
                    entry.GoalsFor,
                    entry.GoalsAgainst,
                    entry.GoalDifference,
                    entry.Points,
                    position,
                    table.Qualifies(position));
            })
            .ToList();
}
