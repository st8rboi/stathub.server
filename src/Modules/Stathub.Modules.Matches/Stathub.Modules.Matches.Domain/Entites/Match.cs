using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Matches.Domain.Entities;

public sealed class Match : AggregateRoot
{
    public Guid TournamentId { get; private set; }
    public Guid HomeTeamId { get; private set; }
    public Guid AwayTeamId { get; private set; }
    public DateTime StartTimeUtc { get; private set; }
    public MatchStatus Status { get; private set; }
    public int? HomeScore { get; private set; }
    public int? AwayScore { get; private set; }

    private Match()
    {
    }

    private Match(
        Guid id,
        Guid tournamentId,
        Guid homeTeamId,
        Guid awayTeamId,
        DateTime startTimeUtc)
    {
        Id = id;
        TournamentId = tournamentId;
        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
        StartTimeUtc = startTimeUtc;
        Status = MatchStatus.Scheduled;
    }

    public static Match Create(
        Guid tournamentId,
        Guid homeTeamId,
        Guid awayTeamId,
        DateTime startTimeUtc)
    {
        if (tournamentId == Guid.Empty)
            throw new DomainException("Tournament id обязательное поле.");

        if (homeTeamId == Guid.Empty)
            throw new DomainException("Home team id обязательное поле.");

        if (awayTeamId == Guid.Empty)
            throw new DomainException("Away team id обязательное поле.");

        if (startTimeUtc == default)
            throw new DomainException("Start time обязательное поле.");

        return new Match(
            id: Guid.NewGuid(),
            tournamentId: tournamentId,
            homeTeamId: homeTeamId,
            awayTeamId: awayTeamId,
            startTimeUtc: startTimeUtc);
    }
}