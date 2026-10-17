using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Leagues.Domain.Enums;

namespace Stathub.Modules.Leagues.Infrastructure.Services;

internal sealed class StagePublicReader(
    IStageRepository stageRepository,
    ITournamentRepository tournamentRepository,
    ILeagueRepository leagueRepository) : IStagePublicReader
{
    public async Task<StagePublicInfoDto?> GetAsync(Guid stageId, CancellationToken cancellationToken = default)
    {
        var stage = await stageRepository.GetByIdAsync(stageId, cancellationToken);
        if (stage is null)
            return null;

        var tournament = await tournamentRepository.GetByIdAsync(stage.TournamentId, cancellationToken);
        if (tournament is null)
            return null;

        var league = await leagueRepository.GetByIdAsync(tournament.LeagueId, cancellationToken);
        if (league is null)
            return null;

        return new StagePublicInfoDto(
            stage.Id,
            stage.Name,
            stage.Order,
            stage.FormatType.ToString(),
            tournament.Id,
            tournament.Name,
            league.Id,
            league.Name,
            league.Slug,
            league.LogoUrl,
            stage.PointsRule.WinPoints,
            stage.PointsRule.DrawPoints,
            stage.PointsRule.LossPoints,
            stage.TiebreakerRules.OrderBy(rule => rule.Priority).Select(rule => Map(rule.Criterion)).ToList());
    }

    private static TiebreakerKind Map(TiebreakerCriterion criterion) => criterion switch
    {
        TiebreakerCriterion.Points => TiebreakerKind.Points,
        TiebreakerCriterion.HeadToHead => TiebreakerKind.HeadToHead,
        TiebreakerCriterion.GoalDifference => TiebreakerKind.GoalDifference,
        TiebreakerCriterion.GoalsScored => TiebreakerKind.GoalsScored,
        _ => throw new ArgumentOutOfRangeException(nameof(criterion), criterion, "Неизвестный критерий тай-брейка.")
    };
}
