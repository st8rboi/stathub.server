using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Matches.Domain.Entities;

public sealed record MatchFormat(
    int PeriodsCount,
    int PeriodDurationMinutes,
    bool ExtraTimeEnabled,
    bool PenaltyShootoutEnabled)
{
    public static MatchFormat Create(
        int periodsCount,
        int periodDurationMinutes,
        bool extraTimeEnabled,
        bool penaltyShootoutEnabled)
    {
        if (periodsCount <= 0)
            throw new DomainException("Количество периодов должно быть больше 0.");

        if (periodDurationMinutes <= 0)
            throw new DomainException("Продолжительность периода должна быть больше 0.");

        return new MatchFormat(
                periodsCount,
                periodDurationMinutes,
                extraTimeEnabled,
                penaltyShootoutEnabled);
    }
}