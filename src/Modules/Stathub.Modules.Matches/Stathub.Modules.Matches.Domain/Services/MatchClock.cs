using Stathub.Modules.Matches.Domain.Enums;

namespace Stathub.Modules.Matches.Domain.Services;

/// <summary>Игровое время события: номер периода и секунды от его начала без учёта пауз.</summary>
public sealed record MatchClockMark(int Period, int ElapsedSeconds);

/// <summary>
/// Секундомер матча, восстановленный по логу событий. Правила те же, что у живых часов
/// <see cref="Entities.Match"/>: паузы внутри периода не считаются игровым временем,
/// новый период обнуляет часы; если матч стоял на паузе, часы нового периода стоят до продолжения.
/// Это производная от событий (read-model), а не хранимая «истина» — см. AGENTS.md.
/// </summary>
public static class MatchClock
{
    /// <summary>
    /// Отметки времени для каждого события лога (в том же порядке).
    /// У событий жизненного цикла и у событий до начала матча отметки нет (<c>null</c>).
    /// </summary>
    public static IReadOnlyList<MatchClockMark?> Compute(
        IReadOnlyList<(MatchEventType Type, DateTime OccurredAtUtc)> timeline)
    {
        var marks = new MatchClockMark?[timeline.Count];

        var period = 0;
        DateTime periodStartedAtUtc = default;
        DateTime? pausedAtUtc = null;
        var pausedMilliseconds = 0d;

        for (var index = 0; index < timeline.Count; index++)
        {
            var (type, occurredAtUtc) = timeline[index];

            switch (type)
            {
                case MatchEventType.MatchStarted:
                    period = 1;
                    periodStartedAtUtc = occurredAtUtc;
                    pausedAtUtc = null;
                    pausedMilliseconds = 0;
                    break;

                case MatchEventType.MatchPaused:
                    pausedAtUtc ??= occurredAtUtc;
                    break;

                case MatchEventType.MatchResumed:
                    if (pausedAtUtc is { } pausedAt)
                        pausedMilliseconds += Math.Max(0, (occurredAtUtc - pausedAt).TotalMilliseconds);
                    pausedAtUtc = null;
                    break;

                case MatchEventType.PeriodStarted:
                    period++;
                    periodStartedAtUtc = occurredAtUtc;
                    pausedMilliseconds = 0;
                    pausedAtUtc = pausedAtUtc is null ? null : occurredAtUtc;
                    break;

                case MatchEventType.MatchFinished:
                    break;

                default:
                    if (period == 0)
                        break;

                    // Событие во время паузы фиксируется на минуте остановки.
                    var clockAtUtc = pausedAtUtc is { } stoppedAt && stoppedAt < occurredAtUtc ? stoppedAt : occurredAtUtc;
                    var playedMilliseconds = (clockAtUtc - periodStartedAtUtc).TotalMilliseconds - pausedMilliseconds;

                    marks[index] = new MatchClockMark(period, (int)Math.Max(0, playedMilliseconds / 1000));
                    break;
            }
        }

        return marks;
    }
}
