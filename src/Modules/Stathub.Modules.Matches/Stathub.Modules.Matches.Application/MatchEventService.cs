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
    IMatchEventRepository eventRepository,
    IPlayerEligibility playerEligibility)
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
    /// Принимает батч событий. Судья на поле не может отменить случившееся, поэтому батч не падает
    /// целиком: повторы отбрасываются, технически невозможные события возвращаются с причиной,
    /// а всё остальное записывается одним SaveChanges вместе с пересчитанным счётом.
    /// </summary>
    /// <param name="matchId">Матч</param>
    /// <param name="incoming">События из очереди устройства</param>
    /// <param name="clientSentAtUtc">
    /// Часы устройства в момент отправки: разница с часами сервера — поправка ко времени всех
    /// событий батча. Работает и при отправке через час после матча, если часы не переводили.
    /// </param>
    /// <param name="cancellationToken"></param>
    public async Task<AppendEventsResultDto> AppendEventsAsync(
        Guid matchId,
        IReadOnlyList<NewMatchEvent> incoming,
        DateTime? clientSentAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (incoming.Count == 0)
            throw new DomainException("Список событий пуст.");

        var match = await GetMatchOrThrowAsync(matchId, cancellationToken);

        var existing = await eventRepository.GetByMatchIdAsync(matchId, cancellationToken);
        var eventsById = existing.ToDictionary(matchEvent => matchEvent.Id);

        var nowUtc = DateTime.UtcNow;
        var clockOffset = clientSentAtUtc is { } sentAtUtc ? nowUtc - sentAtUtc : TimeSpan.Zero;

        var accepted = new List<MatchEvent>();
        var duplicateIds = new List<Guid>();
        var rejected = new List<RejectedEventDto>();

        // Статус меняется переходами, поэтому батч применяется в порядке, назначенном устройством.
        foreach (var item in incoming.OrderBy(item => item.SequenceNumber).ThenBy(item => item.ClientTimestampUtc))
        {
            // Проверка до правил матча: повтор уже принятого финального свистка — не ошибка.
            if (eventsById.ContainsKey(item.Id))
            {
                duplicateIds.Add(item.Id);
                continue;
            }

            try
            {
                var matchEvent = MatchEvent.Create(
                    item.Id,
                    matchId,
                    item.Type,
                    item.TeamId,
                    item.PlayerId,
                    item.RelatedPlayerId,
                    item.TargetEventId,
                    item.Reason,
                    item.ClientTimestampUtc,
                    item.SequenceNumber,
                    CorrectTime(item.ClientTimestampUtc, clockOffset, nowUtc));

                match.EnsureCanAccept(matchEvent);
                EnsureVoidTargetIsValid(matchEvent, eventsById);

                if (matchEvent.IsLifecycle)
                    match.ApplyLifecycle(matchEvent);

                await MarkIneligiblePlayersAsync(match, matchEvent, cancellationToken);

                accepted.Add(matchEvent);
                eventsById.Add(matchEvent.Id, matchEvent);
            }
            catch (DomainException exception)
            {
                rejected.Add(new RejectedEventDto(item.Id, exception.Message));
            }
        }

        var allEvents = existing.Concat(accepted).ToList();

        if (accepted.Count > 0)
        {
            match.RebuildScoreProjection(allEvents);

            await eventRepository.AddRangeAsync(accepted, cancellationToken);
            // Матч и события живут в одном DbContext, поэтому одно сохранение на оба агрегата.
            await eventRepository.SaveChangesAsync(cancellationToken);
        }

        return new AppendEventsResultDto(
            accepted.Select(matchEvent => matchEvent.Id).ToList(),
            duplicateIds,
            rejected,
            BuildState(match, allEvents));
    }

    /// <summary>Будущего времени быть не может: это значит, что часы перевели во время матча.</summary>
    private static DateTime CorrectTime(DateTime clientTimeUtc, TimeSpan clockOffset, DateTime nowUtc)
    {
        var corrected = clientTimeUtc + clockOffset;
        return corrected > nowUtc ? nowUtc : corrected;
    }

    /// <summary>
    /// Дисквалифицированный игрок на поле — факт, который судья не может отменить, а список
    /// дисквалификаций у него мог устареть без связи. Событие принимается с пометкой,
    /// разбирается организатор. Проверка — по БД на каждый батч, снятие действует сразу.
    /// </summary>
    private async Task MarkIneligiblePlayersAsync(
        Match match,
        MatchEvent matchEvent,
        CancellationToken cancellationToken)
    {
        if (matchEvent.TeamId is not { } teamId)
            return;

        foreach (var playerId in new[] { matchEvent.PlayerId, matchEvent.RelatedPlayerId })
        {
            if (playerId is not { } id)
                continue;

            var left = await playerEligibility.GetBanLeftAsync(
                match.TournamentId,
                teamId,
                id,
                cancellationToken);

            if (left is null)
                continue;

            matchEvent.MarkForReview(left == int.MaxValue
                ? "Участвовал игрок с бессрочной дисквалификацией."
                : $"Участвовал дисквалифицированный игрок: осталось матчей — {left}.");

            return;
        }
    }

    private static void EnsureVoidTargetIsValid(
        MatchEvent matchEvent,
        IReadOnlyDictionary<Guid, MatchEvent> eventsById)
    {
        if (matchEvent.Type != MatchEventType.EventVoided)
            return;

        // Наличие TargetEventId уже проверено в MatchEvent.Create.
        if (!eventsById.TryGetValue(matchEvent.TargetEventId!.Value, out var target))
            throw new DomainException("Отменяемое событие не найдено в протоколе матча.");

        if (target.Type == MatchEventType.EventVoided)
            throw new DomainException("Нельзя отменить событие отмены: отмена — часть протокола, а не состояние.");

        if (target.IsLifecycle)
            throw new DomainException("Смену статуса матча отменить нельзя.");
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
