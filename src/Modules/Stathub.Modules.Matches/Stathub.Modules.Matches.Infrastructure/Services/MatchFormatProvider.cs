using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Matches.Application.Dtos;
using Stathub.Modules.Matches.Application.Interfaces;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Matches.Infrastructure.Services;

public sealed class MatchFormatProvider(
    IStageReader stageReader) : IMatchFormatProvider
{
    public async Task<MatchFormatDto> GetByStageIdAsync(
        Guid stageId,
        CancellationToken cancellationToken = default)
    {
        var rules = await stageReader.GetMatchRulesAsync(
            stageId,
            cancellationToken);

        if (rules is null)
            throw new NotFoundException(
                $"Правила матча для стадии '{stageId}' не найдены.");

        return new MatchFormatDto(
            rules.PeriodDurationMinutes,
            rules.PeriodsCount,
            rules.ExtraTimeEnabled,
            rules.PenaltyShootoutEnabled);
    }
}