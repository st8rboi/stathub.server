namespace Stathub.Modules.Leagues.Application.Interfaces;

public interface IStageReader
{
    Task<StageMatchRulesDto?> GetMatchRulesAsync(
        Guid stageId,
        CancellationToken cancellationToken);
}