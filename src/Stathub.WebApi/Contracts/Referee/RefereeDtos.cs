namespace Stathub.WebApi.Contracts.Referee;

/// <summary>Матч в списке «Мои матчи» судьи: достаточно для списка и перехода в матч.</summary>
public sealed record RefereeMatchDto(
    Guid Id,
    Guid StageId,
    string StageName,
    string TournamentName,
    string LeagueName,
    Guid HomeTeamId,
    string HomeTeamName,
    Guid AwayTeamId,
    string AwayTeamName,
    string Status,
    int HomeScore,
    int AwayScore,
    int CurrentPeriod,
    int PeriodsCount,
    DateTime ScheduledTimeUtc,
    DateTime? StartedAtUtc);
