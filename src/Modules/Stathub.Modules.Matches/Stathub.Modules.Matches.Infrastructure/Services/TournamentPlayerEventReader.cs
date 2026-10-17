using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Matches.Application.Dtos;
using Stathub.Modules.Matches.Application.Enums;
using Stathub.Modules.Matches.Application.Interfaces;
using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Modules.Matches.Infrastructure.Persistence;

namespace Stathub.Modules.Matches.Infrastructure.Services;

/// <summary>
/// События игроков турнира одним запросом. Отмены вычисляются здесь же: в БД флага
/// «отменено» нет, состояние выводится из лога (`EventVoided` → `TargetEventId`).
/// </summary>
internal sealed class TournamentPlayerEventReader(MatchesDbContext dbContext) : ITournamentPlayerEventReader
{
    private static readonly Dictionary<MatchEventType, TournamentPlayerEventKind> KindMap = new()
    {
        [MatchEventType.Goal] = TournamentPlayerEventKind.Goal,
        [MatchEventType.OwnGoal] = TournamentPlayerEventKind.OwnGoal,
        [MatchEventType.YellowCard] = TournamentPlayerEventKind.YellowCard,
        [MatchEventType.RedCard] = TournamentPlayerEventKind.RedCard
    };

    public async Task<IReadOnlyList<TournamentPlayerEventDto>> GetAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        var events = await dbContext.MatchEvents
            .AsNoTracking()
            .Where(matchEvent => dbContext.Matches
                .Any(match => match.Id == matchEvent.MatchId && match.TournamentId == tournamentId))
            .Select(matchEvent => new
            {
                matchEvent.Id,
                matchEvent.MatchId,
                matchEvent.Type,
                matchEvent.PlayerId,
                matchEvent.RelatedPlayerId,
                matchEvent.TeamId,
                matchEvent.TargetEventId
            })
            .ToListAsync(cancellationToken);

        var voidedIds = events
            .Where(matchEvent => matchEvent.Type == MatchEventType.EventVoided && matchEvent.TargetEventId is not null)
            .Select(matchEvent => matchEvent.TargetEventId!.Value)
            .ToHashSet();

        return events
            .Where(matchEvent => (matchEvent.PlayerId.HasValue || matchEvent.RelatedPlayerId.HasValue)
                                 && !voidedIds.Contains(matchEvent.Id)
                                 && KindMap.ContainsKey(matchEvent.Type))
            .Select(matchEvent => new TournamentPlayerEventDto(
                matchEvent.MatchId,
                matchEvent.PlayerId,
                matchEvent.TeamId,
                matchEvent.RelatedPlayerId,
                KindMap[matchEvent.Type]))
            .ToList();
    }
}
