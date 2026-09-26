using Stathub.Modules.Matches.Application.Interfaces;
using Stathub.Modules.Matches.Application.Dtos;
using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Matches.Application;

public sealed class MatchService(IMatchRepository repository)
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
    public async Task StartMatchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var match = await GetOrThrowAsync(id, cancellationToken);
        var format = await matchFormatProvider.GetByStageIdAsync(
            match.StageId,
            cancellationToken);

        match.Start(format);

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task StopMatchAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var match = await GetOrThrowAsync(id, cancellationToken);
        var format = await matchFormatProvider.GetByStageIdAsync(
            match.StageId,
            cancellationToken);

        match.Stop(format);

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ResumeMatchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var match = await GetOrThrowAsync(id, cancellationToken);
        var format = await matchFormatProvider.GetByStageIdAsync(
            match.StageId,
            cancellationToken);

        match.Resume(format);

        await repository.SaveChangesAsync(cancellationToken);    
    }

    public async Task FinishMatchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var match = await GetOrThrowAsync(id, cancellationToken);
        var format = await matchFormatProvider.GetByStageIdAsync(
            match.StageId,
            cancellationToken);

        match.Finish(format);

        await repository.SaveChangesAsync(cancellationToken);    
    }
    #endregion  


    #region Вспомогательные методы
    private async Task<Match> GetOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Матч '{id}' не найден.");

    #endregion
}
