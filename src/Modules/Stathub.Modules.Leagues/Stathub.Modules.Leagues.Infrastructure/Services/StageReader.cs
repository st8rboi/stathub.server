using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Leagues.Application.Dtos;

namespace Stathub.Modules.Leagues.Infrastructure.Services;

public sealed class StageReader(IStageRepository repository) : IStageReader
{
    // Получить правила матча
    public async Task<StageMatchRulesDto?> GetMatchRulesAsync(
        Guid stageId,
        CancellationToken cancellationToken = default)
    {
        var stage = await repository.GetByIdAsync(
            stageId,
            cancellationToken);

        if (stage is null)
            return null;

        var rule = stage.MatchFormatRule;
        
        return new StageMatchRulesDto(
            TournamentId: stage.TournamentId,
            DivisionId: stage.DivisionId,
            PeriodsCount: rule.PeriodsCount,
            PeriodDurationMinutes: rule.PeriodDurationMinutes,
            ExtraTimeEnabled: rule.ExtraTimeEnabled,
            PenaltyShootoutEnabled: rule.PenaltyShootoutEnabled,
            Groups: stage.Groups
                .OrderBy(group => group.Order)
                .Select(group => new StageGroupTeamsDto(group.Id, group.Name, group.Order, group.TeamIds.ToList()))
                .ToList());
    }
}