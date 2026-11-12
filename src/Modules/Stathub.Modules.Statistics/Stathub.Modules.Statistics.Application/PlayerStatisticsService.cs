using Stathub.Modules.Statistics.Application.Dtos;
using Stathub.Modules.Statistics.Application.Interfaces;
using Stathub.Modules.Statistics.Domain;
using Stathub.Modules.Statistics.Domain.Models;

namespace Stathub.Modules.Statistics.Application;

/// <summary>
/// Статистика игроков по дивизиону. Дивизионы одного турнира — независимые соревнования,
/// поэтому бомбардиры и карточки в каждом считаются отдельно; этапы дивизиона (круг, плей-офф)
/// суммируются. Считается на каждый запрос из протокола матчей — отдельного хранилища нет,
/// поэтому показатель всегда совпадает с протоколом (doc §9.2).
/// </summary>
public sealed class PlayerStatisticsService(
    ITournamentPlayerEventsSource eventsSource,
    IPlayerNameSource playerNameSource,
    ITeamBadgeSource teamBadgeSource)
{
    public async Task<IReadOnlyList<PlayerStatisticsRowDto>> GetDivisionStatisticsAsync(
        Guid divisionId,
        CancellationToken cancellationToken = default)
    {
        var events = await eventsSource.GetByDivisionAsync(divisionId, cancellationToken);
        return await ToRowsAsync(PlayerStatisticsCalculator.Calculate(events), cancellationToken);
    }

    /// <summary>
    /// Статистика игроков команды в указанных матчах дивизиона (страница команды).
    /// </summary>
    public async Task<IReadOnlyList<PlayerStatisticsRowDto>> GetTeamStatisticsAsync(
        Guid divisionId,
        Guid teamId,
        IReadOnlyCollection<Guid> matchIds,
        CancellationToken cancellationToken = default)
    {
        if (matchIds.Count == 0)
            return [];

        var events = await eventsSource.GetByDivisionAsync(divisionId, cancellationToken);
        var entries = PlayerStatisticsCalculator.CalculateForTeam(events, teamId, matchIds.ToHashSet());

        return await ToRowsAsync(entries, cancellationToken);
    }

    private async Task<IReadOnlyList<PlayerStatisticsRowDto>> ToRowsAsync(
        IReadOnlyList<PlayerStatisticsEntry> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
            return [];

        var playerIds = entries.Select(entry => entry.PlayerId).ToList();
        var teamIds = entries
            .Where(entry => entry.TeamId.HasValue)
            .Select(entry => entry.TeamId!.Value)
            .Distinct()
            .ToList();

        var players = await playerNameSource.GetNamesAsync(playerIds, cancellationToken);
        var teams = teamIds.Count == 0
            ? new Dictionary<Guid, TeamBadgeDto>()
            : await teamBadgeSource.GetAsync(teamIds, cancellationToken);

        return entries
            .Select(entry =>
            {
                var team = entry.TeamId is { } teamId ? teams.GetValueOrDefault(teamId) : null;

                return new PlayerStatisticsRowDto(
                    entry.PlayerId,
                    PlayerDisplayName.ToDisplay(players.GetValueOrDefault(entry.PlayerId)),
                    entry.TeamId,
                    team?.Name,
                    team?.LogoUrl,
                    entry.Matches,
                    entry.Goals,
                    entry.Assists,
                    entry.OwnGoals,
                    entry.YellowCards,
                    entry.RedCards);
            })
            .ToList();
    }
}
