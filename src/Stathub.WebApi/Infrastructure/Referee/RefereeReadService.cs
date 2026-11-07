using Stathub.Modules.Identity.Application;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Matches.Application;
using Stathub.Modules.Teams.Application;
using Stathub.WebApi.Contracts.Referee;

namespace Stathub.WebApi.Infrastructure.Referee;

/// <summary>
/// «Мои матчи» судьи: назначения берутся из Identity, матчи — из Matches, названия команд — из Teams.
/// Композиция живёт в WebApi, потому что пересекает три модуля и нужна только этому экрану.
/// </summary>
public sealed class RefereeReadService(
    MatchService matchService,
    AccountService accountService,
    TeamService teamService,
    IStagePublicReader stagePublicReader,
    ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<RefereeMatchDto>> ListMyMatchesAsync(CancellationToken cancellationToken)
    {
        var assignedMatchIds = await accountService.GetAssignedMatchIdsAsync(
            currentUser.UserId,
            currentUser.TenantId,
            cancellationToken);

        if (assignedMatchIds.Count == 0)
            return [];

        var matches = await matchService.GetByIdsAsync(assignedMatchIds, cancellationToken);
        if (matches.Count == 0)
            return [];

        var teamIds = matches
            .SelectMany(match => new[] { match.HomeTeamId, match.AwayTeamId })
            .Distinct()
            .ToList();

        var teams = (await teamService.GetByIdsAsync(teamIds, cancellationToken))
            .ToDictionary(team => team.Id);
        var teamName = (Guid id) => teams.TryGetValue(id, out var team) ? team.Name : "Команда";
        var teamLogo = (Guid id) => teams.TryGetValue(id, out var team) ? team.LogoUrl : null;

        var stages = new Dictionary<Guid, string>();
        foreach (var stageId in matches.Select(match => match.StageId).Distinct())
        {
            var stage = await stagePublicReader.GetAsync(stageId, cancellationToken);
            stages[stageId] = stage?.StageName ?? string.Empty;
        }

        return matches
            .Select(match => new RefereeMatchDto(
                match.Id,
                match.StageId,
                stages.GetValueOrDefault(match.StageId, string.Empty),
                string.Empty,
                string.Empty,
                match.HomeTeamId,
                teamName(match.HomeTeamId),
                match.AwayTeamId,
                teamName(match.AwayTeamId),
                match.Status.ToString(),
                match.HomeScore,
                match.AwayScore,
                match.CurrentPeriod,
                match.PeriodsCount,
                match.ScheduledTimeUtc,
                match.StartedAtUtc))
            .ToList();
    }
}
