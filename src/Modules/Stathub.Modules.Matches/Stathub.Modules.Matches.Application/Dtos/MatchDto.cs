using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Shared.Domain;

namespace Stathub.Modules.Matches.Application.Dtos;

public sealed record MatchDto(
    Guid Id,
    Guid StageId,
    Guid HomeTeamId,
    Guid AwayTeamId,
    DateTime CreatedAtUtc,
    DateTime ScheduledTimeUtc,
    DateTime? StartedAtUtc,
    MatchStatus Status,
    int HomeScore,
    int AwayScore
    )
{
    internal static MatchDto From(Match match) => new(
        match.Id,
        match.StageId,
        match.HomeTeamId,
        match.AwayTeamId,
        match.CreatedAtUtc,
        match.ScheduledTimeUtc,
        match.StartedAtUtc,
        match.Status,
        match.HomeScore,
        match.AwayScore
    );
}
