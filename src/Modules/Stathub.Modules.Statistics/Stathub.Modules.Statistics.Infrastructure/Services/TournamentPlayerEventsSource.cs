using Stathub.Modules.Matches.Application.Enums;
using Stathub.Modules.Matches.Application.Interfaces;
using Stathub.Modules.Statistics.Application.Interfaces;
using Stathub.Modules.Statistics.Domain.Enums;
using Stathub.Modules.Statistics.Domain.Models;

namespace Stathub.Modules.Statistics.Infrastructure.Services;

/// <summary>Адаптер «порт статистики → читатель событий турнира модуля Matches».</summary>
public sealed class TournamentPlayerEventsSource(ITournamentPlayerEventReader reader)
    : ITournamentPlayerEventsSource
{
    public async Task<IReadOnlyList<TournamentPlayerEvent>> GetAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        var events = await reader.GetAsync(tournamentId, cancellationToken);

        return events
            .Select(matchEvent => new TournamentPlayerEvent(
                matchEvent.MatchId,
                matchEvent.PlayerId,
                matchEvent.TeamId,
                matchEvent.RelatedPlayerId,
                ToDomainKind(matchEvent.Kind)))
            .ToList();
    }

    private static PlayerEventKind ToDomainKind(TournamentPlayerEventKind kind) => kind switch
    {
        TournamentPlayerEventKind.Goal => PlayerEventKind.Goal,
        TournamentPlayerEventKind.OwnGoal => PlayerEventKind.OwnGoal,
        TournamentPlayerEventKind.YellowCard => PlayerEventKind.YellowCard,
        TournamentPlayerEventKind.RedCard => PlayerEventKind.RedCard,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Неизвестный тип события турнира.")
    };
}
