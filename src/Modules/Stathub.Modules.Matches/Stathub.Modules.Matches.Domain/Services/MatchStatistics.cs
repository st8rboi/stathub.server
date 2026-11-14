using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.Enums;

namespace Stathub.Modules.Matches.Domain.Services;

/// <summary>Командная статистика матча. <see cref="Shots"/> — все удары, включая удары в створ.</summary>
public sealed record TeamMatchStatistics(int Shots, int ShotsOnTarget, int Fouls, int Corners)
{
    public static readonly TeamMatchStatistics Empty = new(0, 0, 0, 0);
}

public sealed record MatchStatistics(TeamMatchStatistics Home, TeamMatchStatistics Away)
{
    /// <summary>По матчу не отмечено ни одного события статистики (или все отменены).</summary>
    public bool IsEmpty => Home == TeamMatchStatistics.Empty && Away == TeamMatchStatistics.Empty;

    /// <summary>
    /// Считает статистику по протоколу: это проекция лога, а не хранимая «истина» (см. AGENTS.md).
    /// Отменённые события не учитываются, удар в створ засчитывается и в общее число ударов.
    /// </summary>
    public static MatchStatistics Compute(Guid homeTeamId, Guid awayTeamId, IReadOnlyCollection<MatchEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var voidedEventIds = events
            .Where(matchEvent => matchEvent.Type == MatchEventType.EventVoided && matchEvent.TargetEventId is not null)
            .Select(matchEvent => matchEvent.TargetEventId!.Value)
            .ToHashSet();

        var home = new Counter();
        var away = new Counter();

        foreach (var matchEvent in events)
        {
            if (!matchEvent.IsStatistic || voidedEventIds.Contains(matchEvent.Id))
                continue;

            var counter = matchEvent.TeamId == homeTeamId ? home
                : matchEvent.TeamId == awayTeamId ? away
                : null;

            counter?.Add(matchEvent.Type);
        }

        return new MatchStatistics(home.ToStatistics(), away.ToStatistics());
    }

    private sealed class Counter
    {
        private int shots;
        private int shotsOnTarget;
        private int fouls;
        private int corners;

        public void Add(MatchEventType type)
        {
            switch (type)
            {
                case MatchEventType.Shot:
                    shots++;
                    break;
                case MatchEventType.ShotOnTarget:
                    shots++;
                    shotsOnTarget++;
                    break;
                case MatchEventType.Foul:
                    fouls++;
                    break;
                case MatchEventType.Corner:
                    corners++;
                    break;
            }
        }

        public TeamMatchStatistics ToStatistics() => new(shots, shotsOnTarget, fouls, corners);
    }
}
