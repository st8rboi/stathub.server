using Stathub.Modules.Statistics.Domain.Enums;
using Stathub.Modules.Statistics.Domain.Models;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Statistics.Domain;

/// <summary>
/// Сводит события турнира в статистику игроков. Чистая функция без БД: вызывается на каждый
/// запрос витрины, поэтому никакого кэша и состояния.
/// <para>
/// События без игрока (гол «без автора», карточка команды) в личную статистику автора не попадают,
/// но ассистент гола учитывается, даже если автор не указан. Отменённые события отфильтрованы
/// до расчёта.
/// </para>
/// </summary>
public static class PlayerStatisticsCalculator
{
    public static IReadOnlyList<PlayerStatisticsEntry> Calculate(IEnumerable<TournamentPlayerEvent> events)
    {
        var accumulators = new Dictionary<Guid, Accumulator>();

        foreach (var matchEvent in events)
        {
            if (matchEvent.PlayerId is { } playerId)
                Accumulate(accumulators, playerId, matchEvent).Apply(matchEvent.Kind);

            // Ассистент — второй игрок гола; засчитываем его данным автора передачи.
            if (matchEvent.Kind == PlayerEventKind.Goal && matchEvent.RelatedPlayerId is { } assistId
                && assistId != matchEvent.PlayerId)
            {
                Accumulate(accumulators, assistId, matchEvent).Apply(PlayerEventKind.Assist);
            }
        }

        return accumulators
            .Select(pair => new PlayerStatisticsEntry(
                pair.Key,
                pair.Value.TeamId,
                pair.Value.Goals,
                pair.Value.OwnGoals,
                pair.Value.Assists,
                pair.Value.YellowCards,
                pair.Value.RedCards,
                pair.Value.MatchIds.Count))
            // Порядок детерминирован: сначала голы, затем передачи, затем матчи и Id.
            .OrderByDescending(entry => entry.Goals)
            .ThenByDescending(entry => entry.Assists)
            .ThenByDescending(entry => entry.Matches)
            .ThenBy(entry => entry.PlayerId)
            .ToList();
    }

    private static Accumulator Accumulate(
        Dictionary<Guid, Accumulator> accumulators,
        Guid playerId,
        TournamentPlayerEvent matchEvent)
    {
        if (!accumulators.TryGetValue(playerId, out var accumulator))
        {
            accumulator = new Accumulator(matchEvent.TeamId);
            accumulators[playerId] = accumulator;
        }

        accumulator.MatchIds.Add(matchEvent.MatchId);

        // Игрок мог сменить клуб внутри турнира — показываем последнюю известную команду.
        accumulator.TeamId ??= matchEvent.TeamId;

        return accumulator;
    }

    private sealed class Accumulator(Guid? teamId)
    {
        public Guid? TeamId { get; set; } = teamId;
        public HashSet<Guid> MatchIds { get; } = [];
        public int Goals { get; private set; }
        public int OwnGoals { get; private set; }
        public int Assists { get; private set; }
        public int YellowCards { get; private set; }
        public int RedCards { get; private set; }

        public void Apply(PlayerEventKind kind)
        {
            switch (kind)
            {
                case PlayerEventKind.Goal:
                    Goals++;
                    break;
                case PlayerEventKind.OwnGoal:
                    OwnGoals++;
                    break;
                case PlayerEventKind.Assist:
                    Assists++;
                    break;
                case PlayerEventKind.YellowCard:
                    YellowCards++;
                    break;
                case PlayerEventKind.RedCard:
                    RedCards++;
                    break;
                default:
                    throw new DomainException($"Неизвестный тип события статистики '{kind}'.");
            }
        }
    }
}
