using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Matches.Domain.ValueObjects;

/// <summary>
/// Регламент матча: сколько периодов и по сколько минут, разрешены ли доп. время и серия пенальти.
/// Правила этапа читаются один раз при создании матча и дальше живут в самом матче (снимок),
/// чтобы правка правил этапа не меняла уже созданные матчи.
/// </summary>
public sealed class MatchFormat
{
    public int PeriodsCount { get; private set; }
    public int PeriodDurationMinutes { get; private set; }
    public bool ExtraTimeEnabled { get; private set; }
    public bool PenaltyShootoutEnabled { get; private set; }

    private MatchFormat()
    {
    }

    private MatchFormat(
        int periodsCount,
        int periodDurationMinutes,
        bool extraTimeEnabled,
        bool penaltyShootoutEnabled)
    {
        PeriodsCount = periodsCount;
        PeriodDurationMinutes = periodDurationMinutes;
        ExtraTimeEnabled = extraTimeEnabled;
        PenaltyShootoutEnabled = penaltyShootoutEnabled;
    }

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
