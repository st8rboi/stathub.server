using Stathub.Modules.Matches.Application.Dtos;

namespace Stathub.Modules.Matches.Application.Interfaces;

public interface IMatchFormatProvider
{
    Task<MatchFormatDto> GetByStageIdAsync(
        Guid stageId,
        CancellationToken cancellationToken = default);
}