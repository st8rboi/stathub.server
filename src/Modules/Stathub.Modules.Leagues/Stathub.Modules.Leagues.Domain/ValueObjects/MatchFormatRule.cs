using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Domain.ValueObjects;

public sealed record MatchFormatRule
{
    public int PeriodDurationMinutes { get; }
    public int PeriodsCount { get; }
    public bool ExtraTimeEnabled { get; }
    public bool PenaltyShootoutEnabled { get; }

    public MatchFormatRule(int periodDurationMinutes, int periodsCount, bool extraTimeEnabled, bool penaltyShootoutEnabled)
    {
        if (periodDurationMinutes <= 0)
            throw new DomainException("Period duration must be positive.");

        if (periodsCount <= 0)
            throw new DomainException("Periods count must be positive.");

        PeriodDurationMinutes = periodDurationMinutes;
        PeriodsCount = periodsCount;
        ExtraTimeEnabled = extraTimeEnabled;
        PenaltyShootoutEnabled = penaltyShootoutEnabled;
    }
}
