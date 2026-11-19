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

        // Идущие матчи входят в расчёт с текущим счётом: таблица «живая» и меняется после каждого гола.
        var matches = await matchSource.GetMatchesAsync(stageId, cancellationToken);
        var tables = StandingsCalculator.CalculateTables(matches, rules);
        var liveByTeam = LiveMatchesByTeam(matches);

        var teamIds = tables
            .SelectMany(table => table.Entries)
            .Select(entry => entry.TeamId)
            .Distinct()
            .ToList();
        var teams = teamIds.Count == 0
            ? new Dictionary<Guid, StandingsTeamDto>()
            : await teamNameSource.GetTeamsAsync(teamIds, cancellationToken);

        var tableRows = tables
            .Select(table => (table, rows: ToRows(table, teams, liveByTeam)))
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
            rules.QualifiersCount,
            rules.EliminatedCount);
    }

    /// <summary>Идущий матч каждой играющей сейчас команды.</summary>
    private static Dictionary<Guid, StandingsMatch> LiveMatchesByTeam(IEnumerable<StandingsMatch> matches)
    {
        var result = new Dictionary<Guid, StandingsMatch>();

        foreach (var match in matches.Where(match => match.IsLive))
        {
            result[match.HomeTeamId] = match;
            result[match.AwayTeamId] = match;
        }

        return result;
    }

    private static List<StandingsRowDto> ToRows(
        StandingsTable table,
        IReadOnlyDictionary<Guid, StandingsTeamDto> teams,
        IReadOnlyDictionary<Guid, StandingsMatch> liveByTeam) =>
        table.Entries
            .Select((entry, index) =>
            {
                var team = teams.GetValueOrDefault(entry.TeamId);
                var position = index + 1;
                var live = liveByTeam.GetValueOrDefault(entry.TeamId) is { } liveMatch
                    ? ToLive(liveMatch, entry.TeamId)
                    : null;

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
                    table.Qualifies(position),
                    table.Eliminated(position),
                    live);
            })
            .ToList();

    private static StandingsLiveMatchDto ToLive(StandingsMatch match, Guid teamId)
    {
        var (scored, conceded) = match.ScoreFor(teamId);
        return new StandingsLiveMatchDto(match.MatchId, scored, conceded, match.OutcomeFor(teamId));
    }
}
