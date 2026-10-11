using Stathub.Modules.Matches.Application.Interfaces;
using Stathub.Modules.Matches.Application.Dtos;
using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.ValueObjects;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Matches.Application;

public sealed class MatchService(IMatchRepository repository, IMatchFormatProvider matchFormatProvider)
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
        var matchFormat = await ResolveMatchFormatAsync(stageId, cancellationToken);

        var match = Match.Create(stageId, homeTeamId, awayTeamId, scheduledTimeUtc, matchFormat);

        await repository.AddAsync(match, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return match.Id;
    }

    public async Task<MatchDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        MatchDto.From(await GetOrThrowAsync(id, cancellationToken));

    #endregion

    #region Управление матчем
    public async Task StartMatchAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var match = await GetOrThrowAsync(id, cancellationToken);

        match.Start();

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task StopMatchAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var match = await GetOrThrowAsync(id, cancellationToken);

        match.Stop();

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ResumeMatchAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var match = await GetOrThrowAsync(id, cancellationToken);

        match.Resume();

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task StartNextPeriodAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var match = await GetOrThrowAsync(id, cancellationToken);

        match.StartNextPeriod();

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task FinishMatchAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var match = await GetOrThrowAsync(id, cancellationToken);

        match.Finish();

        await repository.SaveChangesAsync(cancellationToken);
    }
    #endregion


    #region Вспомогательные методы
    private async Task<Match> GetOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Матч '{id}' не найден.");

    // Правила читаются через интерфейс потребителя (IMatchFormatProvider), реализацию даёт модуль Leagues.
    private async Task<MatchFormat> ResolveMatchFormatAsync(
        Guid stageId,
        CancellationToken cancellationToken)
    {
        var stageFormat = await matchFormatProvider.GetByStageIdAsync(stageId, cancellationToken);

        return MatchFormat.Create(
            periodsCount: stageFormat.PeriodsCount,
            periodDurationMinutes: stageFormat.PeriodDurationMinutes,
            extraTimeEnabled: stageFormat.ExtraTimeEnabled,
            penaltyShootoutEnabled: stageFormat.PenaltyShootoutEnabled);
    }
    #endregion
}
