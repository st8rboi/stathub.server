using Stathub.Modules.Matches.Application.Interfaces;
using Stathub.Modules.Matches.Application.Dtos;
using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Modules.Matches.Domain.ValueObjects;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Matches.Application;

public sealed class MatchService(
    IMatchRepository repository,
    IMatchEventRepository eventRepository,
    IMatchFormatProvider matchFormatProvider)
{
    #region Базовые методы
    /// <summary>
    /// Создаёт матч и снимает в него правила этапа (регламент матча).
    /// Снимок делается один раз здесь: дальнейшая правка правил этапа
    /// уже не влияет на созданный матч.
    /// </summary>
    /// <param name="stageId">Стадия (этап) турнира, из которой берутся правила</param>
    /// <param name="homeTeamId"></param>
    /// <param name="awayTeamId"></param>
    /// <param name="scheduledTimeUtc"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Guid> CreateMatchAsync(
        Guid stageId,
        Guid homeTeamId,
        Guid awayTeamId,
        DateTime scheduledTimeUtc,
        CancellationToken cancellationToken = default)
    {
        var (matchFormat, tournamentId) = await ResolveMatchFormatAsync(stageId, cancellationToken);

        var match = Match.Create(stageId, tournamentId, homeTeamId, awayTeamId, scheduledTimeUtc, matchFormat);

        await repository.AddAsync(match, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return match.Id;
    }

    public async Task<MatchDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        MatchDto.From(await GetOrThrowAsync(id, cancellationToken));

    /// <summary>
    /// Матчи этапа в порядке расписания. Ближайший матч — первый в списке.
    /// </summary>
    /// <param name="stageId">Этап турнира, матчи которого нужно вернуть</param>
    /// <param name="cancellationToken"></param>
    public async Task<IReadOnlyList<MatchDto>> ListByStageAsync(
        Guid stageId,
        CancellationToken cancellationToken = default)
    {
        if (stageId == Guid.Empty)
            throw new DomainException("StageId обязательное поле.");

        var matches = await repository.GetByStageIdAsync(stageId, cancellationToken);

        return matches
            .OrderBy(match => match.ScheduledTimeUtc)
            .Select(MatchDto.From)
            .ToList();
    }

    /// <summary>
    /// Матчи команды во всех этапах в порядке расписания — публичная страница команды.
    /// </summary>
    public async Task<IReadOnlyList<MatchDto>> ListByTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        if (teamId == Guid.Empty)
            throw new DomainException("TeamId обязательное поле.");

        var matches = await repository.GetByTeamIdAsync(teamId, cancellationToken);

        return matches
            .OrderBy(match => match.ScheduledTimeUtc)
            .Select(MatchDto.From)
            .ToList();
    }

    /// <summary>
    /// Матчи, которые идут прямо сейчас (в процессе или на паузе) — публичная live-лента.
    /// </summary>
    public async Task<IReadOnlyList<MatchDto>> ListLiveAsync(CancellationToken cancellationToken = default)
    {
        var matches = await repository.GetByStatusAsync(
            [MatchStatus.InProgress, MatchStatus.Pause],
            cancellationToken);

        return matches
            .OrderBy(match => match.ScheduledTimeUtc)
            .Select(MatchDto.From)
            .ToList();
    }

    /// <summary>Матчи по идентификаторам: используется «моими матчами» судьи.</summary>
    public async Task<IReadOnlyList<MatchDto>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var matches = await repository.GetByIdsAsync(ids, cancellationToken);
        return matches.Select(MatchDto.From).ToList();
    }

    /// <summary>Расписание на интервал: публичная витрина группирует матчи по дням на клиенте.</summary>
    public async Task<IReadOnlyList<MatchDto>> ListByPeriodAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        if (toUtc <= fromUtc)
            throw new DomainException("Конец периода должен быть позже начала.");

        var matches = await repository.GetByPeriodAsync(fromUtc, toUtc, cancellationToken);

        return matches.Select(MatchDto.From).ToList();
    }

    /// <summary>Перенос и замена команд у не начатого матча без событий.</summary>
    public async Task<MatchDto> UpdateAsync(
        Guid id,
        DateTime scheduledTimeUtc,
        Guid homeTeamId,
        Guid awayTeamId,
        CancellationToken cancellationToken = default)
    {
        var match = await GetOrThrowAsync(id, cancellationToken);

        match.Update(scheduledTimeUtc, homeTeamId, awayTeamId, await HasEventsAsync(id, cancellationToken));
        await repository.SaveChangesAsync(cancellationToken);

        return MatchDto.From(match);
    }

    /// <summary>
    /// Проверка до удаления: вызывающий сначала убеждается, что матч можно удалить,
    /// и только потом трогает связанные данные других модулей (взносы).
    /// </summary>
    public async Task EnsureCanDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var match = await GetOrThrowAsync(id, cancellationToken);
        match.EnsureCanDelete(await HasEventsAsync(id, cancellationToken));
    }

    /// <summary>Удаление ошибочно созданного матча: не начатого и без событий.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var match = await GetOrThrowAsync(id, cancellationToken);
        match.EnsureCanDelete(await HasEventsAsync(id, cancellationToken));

        repository.Remove(match);
        await repository.SaveChangesAsync(cancellationToken);
    }

    #endregion

    #region Вспомогательные методы
    // Любое событие в логе (включая отменённые) — признак того, что матч уже вели.
    private async Task<bool> HasEventsAsync(Guid matchId, CancellationToken cancellationToken) =>
        (await eventRepository.GetByMatchIdAsync(matchId, cancellationToken)).Count > 0;

    private async Task<Match> GetOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Матч '{id}' не найден.");

    // Правила читаются через интерфейс потребителя (IMatchFormatProvider), реализацию даёт модуль Leagues.
    // Тот же вызов отдаёт турнир этапа: матч запоминает сезон, к которому относится.
    private async Task<(MatchFormat Format, Guid TournamentId)> ResolveMatchFormatAsync(
        Guid stageId,
        CancellationToken cancellationToken)
    {
        var stageFormat = await matchFormatProvider.GetByStageIdAsync(stageId, cancellationToken);

        var format = MatchFormat.Create(
            periodsCount: stageFormat.PeriodsCount,
            periodDurationMinutes: stageFormat.PeriodDurationMinutes,
            extraTimeEnabled: stageFormat.ExtraTimeEnabled,
            penaltyShootoutEnabled: stageFormat.PenaltyShootoutEnabled);

        return (format, stageFormat.TournamentId);
    }
    #endregion
}
