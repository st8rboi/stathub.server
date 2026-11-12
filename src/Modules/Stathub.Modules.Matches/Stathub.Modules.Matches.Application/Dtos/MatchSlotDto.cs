namespace Stathub.Modules.Matches.Application.Dtos;

/// <summary>
/// Только этап и время начала матча — для полосы дат витрины,
/// где нужны дни с матчами, а не сами матчи.
/// </summary>
public sealed record MatchSlotDto(Guid StageId, DateTime ScheduledTimeUtc);
