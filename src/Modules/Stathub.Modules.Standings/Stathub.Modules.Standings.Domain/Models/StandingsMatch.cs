using Stathub.Modules.Standings.Domain.Enums;

namespace Stathub.Modules.Standings.Domain.Models;

/// <summary>
/// Результат матча в терминах расчёта таблицы. Идущий матч (<paramref name="IsLive"/>) считается
/// так, будто закончился с текущим счётом, — это и есть live-таблица.
/// </summary>
/// <param name="GroupId">Группа этапа, в которой сыгран матч; null — этап без групп.</param>
/// <param name="IsLive">Матч ещё идёт: счёт текущий, а не итоговый.</param>
/// <param name="MatchId">Матч; нужен витрине, чтобы связать live-строку таблицы с матчем.</param>
public sealed record StandingsMatch(
    Guid HomeTeamId,
    Guid AwayTeamId,
    int HomeScore,
    int AwayScore,
    Guid? GroupId = null,
    bool IsLive = false,
    Guid? MatchId = null)
{
    public bool Involves(Guid teamId) => teamId == HomeTeamId || teamId == AwayTeamId;

    /// <summary>Голы команды и её соперника в этом матче.</summary>
    public (int Scored, int Conceded) ScoreFor(Guid teamId)
    {
        if (!Involves(teamId))
            throw new ArgumentException($"Команда '{teamId}' не играет в этом матче.", nameof(teamId));

        return teamId == HomeTeamId ? (HomeScore, AwayScore) : (AwayScore, HomeScore);
    }

    /// <summary>Исход для команды при текущем счёте.</summary>
    public LiveOutcome OutcomeFor(Guid teamId)
    {
        var (scored, conceded) = ScoreFor(teamId);
        return scored > conceded ? LiveOutcome.Win : scored == conceded ? LiveOutcome.Draw : LiveOutcome.Loss;
    }
}
