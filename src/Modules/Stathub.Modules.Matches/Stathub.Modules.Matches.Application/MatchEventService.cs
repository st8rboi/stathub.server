using Stathub.Modules.Matches.Application.Dtos;
using Stathub.Modules.Matches.Application.Interfaces;
using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Matches.Application;

/// <summary>
/// Приём событий протокола матча.
///
/// <para>
/// Идемпотентность: Id события генерирует клиент, поэтому повторная отправка из офлайн-очереди
/// не создаёт дублей — такие события возвращаются в <see cref="AppendEventsResultDto.DuplicateEventIds"/>.
/// </para>
/// <para>
/// Счёт — проекция лога: после приёма батча протокол переигрывается целиком
/// (<see cref="Match.RebuildScoreProjection"/>), поэтому порядок доставки событий не важен,
/// важна только их уникальность. Порядок в ответе задаёт клиентский SequenceNumber.
/// </para>
/// </summary>
public sealed class MatchEventService(
    IMatchRepository matchRepository,
    IMatchEventRepository eventRepository)
{
    public async Task<MatchStateDto> GetStateAsync(
        Guid matchId,
        CancellationToken cancellationToken = default)
    {
        var match = await GetMatchOrThrowAsync(matchId, cancellationToken);
        var events = await eventRepository.GetByMatchIdAsync(matchId, cancellationToken);

        return BuildState(match, events);
    }

    /// <summary>
    /// Принимает батч событий. Повторно присланные события отбрасываются, остальные пишутся
    /// в протокол, после чего пересчитывается проекция счёта — всё в одном SaveChanges.
    /// </summary>
    public async Task<AppendEventsResultDto> AppendEventsAsync(
        Guid matchId,
        IReadOnlyList<NewMatchEvent> incoming,
        CancellationToken cancellationToken = default)
    {
        if (incoming.Count == 0)
            throw new DomainException("Список событий пуст.");

        var match = await GetMatchOrThrowAsync(matchId, cancellationToken);
        match.EnsureEventsCanBeChanged();

        var existing = await eventRepository.GetByMatchIdAsync(matchId, cancellationToken);
        var knownIds = existing.Select(matchEvent => matchEvent.Id).ToHashSet();

        var accepted = new List<MatchEvent>();
        var acceptedIds = new List<Guid>();
        var duplicateIds = new List<Guid>();

        foreach (var item in incoming)
        {
            // Add вернёт false и для события из БД, и для повтора внутри самого батча.
            if (!knownIds.Add(item.Id))
            {
                duplicateIds.Add(item.Id);
                continue;
            }

            accepted.Add(MatchEvent.Create(
                item.Id,
                matchId,
                item.Type,
                item.TeamId,
                item.PlayerId,
                item.RelatedPlayerId,
                item.TargetEventId,
                item.Reason,
                item.ClientTimestampUtc,
                item.SequenceNumber));

            acceptedIds.Add(item.Id);
        }

        var allEvents = existing.Concat(accepted).ToList();

        EnsureVoidTargetsExist(accepted, allEvents);

        // Пересчёт — до сохранения: если событие противоречит матчу, ничего не запишется.
        match.RebuildScoreProjection(allEvents);

        if (accepted.Count > 0)
        {
            await eventRepository.AddRangeAsync(accepted, cancellationToken);
            // Матч и события живут в одном DbContext, поэтому одно сохранение на оба агрегата.
            await eventRepository.SaveChangesAsync(cancellationToken);
        }

        return new AppendEventsResultDto(acceptedIds, duplicateIds, BuildState(match, allEvents));
    }

    private static void EnsureVoidTargetsExist(
        IReadOnlyList<MatchEvent> accepted,
        IReadOnlyCollection<MatchEvent> allEvents)
    {
        var eventsById = allEvents.ToDictionary(matchEvent => matchEvent.Id);

        foreach (var matchEvent in accepted)
        {
            if (matchEvent.Type != MatchEventType.EventVoided)
                continue;

            // Наличие TargetEventId уже проверено в MatchEvent.Create.
            var targetId = matchEvent.TargetEventId!.Value;

            if (!eventsById.TryGetValue(targetId, out var target))
                throw new DomainException("Отменяемое событие не найдено в протоколе матча.");

            if (target.Type == MatchEventType.EventVoided)
                throw new DomainException("Нельзя отменить событие отмены: отмена — часть протокола, а не состояние.");
        }
    }

    private static MatchStateDto BuildState(Match match, IReadOnlyCollection<MatchEvent> events)
    {
        var voidedEventIds = events
            .Where(matchEvent => matchEvent.Type == MatchEventType.EventVoided && matchEvent.TargetEventId is not null)
            .Select(matchEvent => matchEvent.TargetEventId!.Value)
            .ToHashSet();

        var eventDtos = events
            .OrderBy(matchEvent => matchEvent.SequenceNumber)
            .ThenBy(matchEvent => matchEvent.ClientTimestampUtc)
            .Select(matchEvent => MatchEventDto.From(matchEvent, voidedEventIds))
            .ToList();

        return new MatchStateDto(MatchDto.From(match), eventDtos);
    }

    private async Task<Match> GetMatchOrThrowAsync(Guid matchId, CancellationToken cancellationToken) =>
        await matchRepository.GetByIdAsync(matchId, cancellationToken)
        ?? throw new NotFoundException($"Матч '{matchId}' не найден.");
}
