using Stathub.Modules.Standings.Domain.Enums;
using Stathub.Modules.Standings.Domain.Models;

namespace Stathub.Modules.Standings.Domain;

/// <summary>
/// Расчёт турнирной таблицы этапа из результатов матчей. Чистая функция без состояния:
/// таблица нигде не хранится и пересчитывается на каждый запрос (documentation/doc.md §9.2),
/// поэтому логики инвалидации нет вовсе.
/// </summary>
public static class StandingsCalculator
{
    public static IReadOnlyList<StandingsEntry> Calculate(
        IReadOnlyCollection<StandingsMatch> matches,
        StandingsRules rules)
    {
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(rules);

        var table = new Dictionary<Guid, Accumulator>();

        foreach (var match in matches)
        {
            var home = GetOrCreate(table, match.HomeTeamId);
            var away = GetOrCreate(table, match.AwayTeamId);

            home.AddResult(match.HomeScore, match.AwayScore, PointsFor(match.HomeScore, match.AwayScore, rules));
            away.AddResult(match.AwayScore, match.HomeScore, PointsFor(match.AwayScore, match.HomeScore, rules));
        }

        var entries = table.Values.Select(accumulator => accumulator.ToEntry()).ToList();
        entries.Sort(new StandingsComparer(rules, matches));

        return entries;
    }

    private static int PointsFor(int scored, int conceded, StandingsRules rules) =>
        scored > conceded
            ? rules.WinPoints
            : scored == conceded
                ? rules.DrawPoints
                : rules.LossPoints;

    private static Accumulator GetOrCreate(Dictionary<Guid, Accumulator> table, Guid teamId)
    {
        if (table.TryGetValue(teamId, out var accumulator))
            return accumulator;

        accumulator = new Accumulator(teamId);
        table[teamId] = accumulator;
        return accumulator;
    }

    private sealed class Accumulator(Guid teamId)
    {
        private int _played;
        private int _wins;
        private int _draws;
        private int _losses;
        private int _goalsFor;
        private int _goalsAgainst;
        private int _points;

        public void AddResult(int scored, int conceded, int points)
        {
            _played++;
            _goalsFor += scored;
            _goalsAgainst += conceded;
            _points += points;

            if (scored > conceded) _wins++;
            else if (scored == conceded) _draws++;
            else _losses++;
        }

        public StandingsEntry ToEntry() => new(
            teamId,
            _played,
            _wins,
            _draws,
            _losses,
            _goalsFor,
            _goalsAgainst,
            _goalsFor - _goalsAgainst,
            _points);
    }

    /// <summary>
    /// Порядок: очки, затем тай-брейки этапа в заданном приоритете, затем стабильный
    /// детерминированный ключ (Id команды), чтобы таблица не «прыгала» между запросами.
    /// </summary>
    private sealed class StandingsComparer(
        StandingsRules rules,
        IReadOnlyCollection<StandingsMatch> matches) : IComparer<StandingsEntry>
    {
        public int Compare(StandingsEntry? left, StandingsEntry? right)
        {
            if (left is null || right is null) return 0;

            var byPoints = right.Points.CompareTo(left.Points);
            if (byPoints != 0) return byPoints;

            foreach (var tiebreaker in rules.Tiebreakers)
            {
                var result = tiebreaker switch
                {
                    StandingsTiebreaker.GoalDifference => right.GoalDifference.CompareTo(left.GoalDifference),
                    StandingsTiebreaker.GoalsScored => right.GoalsFor.CompareTo(left.GoalsFor),
                    StandingsTiebreaker.HeadToHead => CompareHeadToHead(left.TeamId, right.TeamId),
                    _ => 0
                };

                if (result != 0) return result;
            }

            return left.TeamId.CompareTo(right.TeamId);
        }

        /// <summary>Личные встречи: сначала очки в них, затем разница мячей в них.</summary>
        private int CompareHeadToHead(Guid leftTeamId, Guid rightTeamId)
        {
            var leftPoints = 0;
            var rightPoints = 0;
            var leftDifference = 0;

            foreach (var match in matches)
            {
                var isDirect =
                    (match.HomeTeamId == leftTeamId && match.AwayTeamId == rightTeamId) ||
                    (match.HomeTeamId == rightTeamId && match.AwayTeamId == leftTeamId);

                if (!isDirect) continue;

                var leftGoals = match.HomeTeamId == leftTeamId ? match.HomeScore : match.AwayScore;
                var rightGoals = match.HomeTeamId == rightTeamId ? match.HomeScore : match.AwayScore;

                leftDifference += leftGoals - rightGoals;
                leftPoints += PointsFor(leftGoals, rightGoals, rules);
                rightPoints += PointsFor(rightGoals, leftGoals, rules);
            }

            var byHeadToHeadPoints = rightPoints.CompareTo(leftPoints);
            if (byHeadToHeadPoints != 0) return byHeadToHeadPoints;

            // Разница мячей в личных встречах: у правой команды она противоположна левой.
            var rightDifference = -leftDifference;
            return rightDifference.CompareTo(leftDifference);
        }
    }
}
