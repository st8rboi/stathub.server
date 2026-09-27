using Stathub.Modules.Matches.Application.Interfaces;
using Stathub.Modules.Matches.Application.Dtos;
using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Matches.Application;

public sealed class MatchService(IMatchRepository repository, IMatchFormatProvider matchFormatProvider)
{
    #region Базовые методы
    public async Task<Guid> CreateMatchAsync(
        Guid stageId,
        Guid homeTeamId,
        Guid awayTeamId,
        DateTime scheduledTimeUtc,
        CancellationToken cancellationToken = default)
    {
        var match = Match.Create(stageId, homeTeamId, awayTeamId, scheduledTimeUtc);

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
        var matchFormat = await GetMatchFormatAsync(match, cancellationToken);

        match.Start(matchFormat);

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task StopMatchAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var match = await GetOrThrowAsync(id, cancellationToken);
        var matchFormat = await GetMatchFormatAsync(match, cancellationToken);

        match.Stop(matchFormat);

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ResumeMatchAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var match = await GetOrThrowAsync(id, cancellationToken);
        var matchFormat = await GetMatchFormatAsync(match, cancellationToken);

        match.Resume(matchFormat);

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task FinishMatchAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var match = await GetOrThrowAsync(id, cancellationToken);
        var matchFormat = await GetMatchFormatAsync(match, cancellationToken);

        match.Finish(matchFormat);

        await repository.SaveChangesAsync(cancellationToken);
    }
    #endregion  


    #region Вспомогательные методы
    private async Task<Match> GetOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Матч '{id}' не найден.");
    private async Task<MatchFormat> GetMatchFormatAsync(Match match, CancellationToken cancellationToken)
    {
        var stageFormat = await matchFormatProvider.GetByStageIdAsync(
            match.StageId,
            cancellationToken);

        return MatchFormat.Create(
            stageFormat.PeriodsCount,
            stageFormat.PeriodDurationMinutes,
            stageFormat.ExtraTimeEnabled,
            stageFormat.PenaltyShootoutEnabled);
    }
    #endregion
}
