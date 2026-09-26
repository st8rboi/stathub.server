namespace Stathub.Modules.Leagues.Application;

public sealed class StageReader(IStageRepository repository) : IStageReader
{
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
            rule.PeriodDurationMinutes,
            rule.PeriodsCount,
            rule.ExtraTimeEnabled,
            rule.PenaltyShootoutEnabled);
    }
}