using Stathub.Modules.Standings.Application.Dtos;
using Stathub.Modules.Standings.Application.Interfaces;
using Stathub.Modules.Standings.Domain;
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
        var entries = StandingsCalculator.Calculate(matches, rules);

        var teamIds = entries.Select(entry => entry.TeamId).ToList();
        var teams = teamIds.Count == 0
            ? new Dictionary<Guid, StandingsTeamDto>()
            : await teamNameSource.GetTeamsAsync(teamIds, cancellationToken);

        var rows = entries
            .Select(entry =>
            {
                var team = teams.GetValueOrDefault(entry.TeamId);

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
                    entry.Points);
            })
            .ToList();

        return new StageStandingsDto(stageId, rows);
    }
}
