using Stathub.Modules.Rosters.Application.Dtos;
using Stathub.Modules.Rosters.Application.Interfaces;
using Stathub.Modules.Rosters.Domain.Entities;
using Stathub.Modules.Rosters.Domain.Enums;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Rosters.Application;

/// <summary>
/// Заявки команд и игроков на этап турнира (группа/дивизион/плей-офф).
/// <para>
/// Модуль ссылается на этап, турнир, команду и игрока только по <c>Guid</c> и проверяет
/// их существование через читатели модулей-поставщиков: межмодульные FK запрещены (AGENTS.md).
/// </para>
/// <para>
/// Правила: команда заявляется один раз на этап (в разных дивизионах может играть одна и та же
/// команда); игрок — не больше одной активной заявки в пределах этапа, но может играть
/// в нескольких дивизионах одного турнира; менять заявки можно, пока турнир не завершён.
/// </para>
/// </summary>
public sealed class RosterService(
    ITournamentTeamRepository repository,
    IStageInfoProvider stageInfo,
    ITeamExistsProvider teamExists,
    IPlayerExistsProvider playerExists,
    IEnumerable<IRosterUsageSource> usageSources)
{
    public async Task<Guid> AddTeamAsync(
        Guid stageId,
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var stage = await GetStageOrThrowAsync(stageId, cancellationToken);
        EnsureRosterChangeable(stage);

        if (!await teamExists.ExistsAsync(teamId, cancellationToken))
            throw new NotFoundException($"Команда '{teamId}' не найдена.");

        if (await repository.GetByStageAndTeamAsync(stageId, teamId, cancellationToken) is not null)
            throw new ConflictException("Команда уже заявлена на этот этап.");

        var tournamentTeam = TournamentTeam.Create(stageId, stage.TournamentId, teamId);

        await repository.AddAsync(tournamentTeam, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return tournamentTeam.Id;
    }

    public async Task<IReadOnlyList<TournamentTeamDto>> ListTeamsAsync(
        Guid stageId,
        CancellationToken cancellationToken = default)
    {
        var teams = await repository.ListByStageAsync(stageId, cancellationToken);
        return teams.Select(TournamentTeamDto.From).ToList();
    }

    public async Task<TournamentTeamDetailsDto> GetTeamAsync(
        Guid stageId,
        Guid tournamentTeamId,
        CancellationToken cancellationToken = default)
    {
        var tournamentTeam = await GetTeamOrThrowAsync(stageId, tournamentTeamId, cancellationToken);
        return TournamentTeamDetailsDto.From(tournamentTeam);
    }

    public async Task<Guid> RegisterPlayerAsync(
        Guid stageId,
        Guid tournamentTeamId,
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var stage = await GetStageOrThrowAsync(stageId, cancellationToken);
        EnsureRosterChangeable(stage);

        var tournamentTeam = await GetTeamOrThrowAsync(stageId, tournamentTeamId, cancellationToken);

        if (!await playerExists.ExistsAsync(playerId, cancellationToken))
            throw new NotFoundException($"Игрок '{playerId}' не найден.");

        if (await repository.FindTeamIdWithActiveRegistrationAsync(stageId, playerId, cancellationToken) is not null)
            throw new ConflictException(
                "Игрок уже заявлен за другую команду этого этапа — оформите трансфер вместо новой заявки.");

        var registration = tournamentTeam.RegisterPlayer(
            playerId,
            DateOnly.FromDateTime(DateTime.UtcNow));

        await repository.SaveChangesAsync(cancellationToken);

        return registration.Id;
    }

    /// <summary>Отзаявка игрока: заявка закрывается, но остаётся в истории.</summary>
    public async Task ReleasePlayerAsync(
        Guid stageId,
        Guid tournamentTeamId,
        Guid registrationId,
        CancellationToken cancellationToken = default)
    {
        var stage = await GetStageOrThrowAsync(stageId, cancellationToken);
        EnsureRosterChangeable(stage);

        var tournamentTeam = await GetTeamOrThrowAsync(stageId, tournamentTeamId, cancellationToken);
        tournamentTeam.ReleasePlayer(registrationId, DateOnly.FromDateTime(DateTime.UtcNow));

        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Трансфер внутри этапа: закрывает заявку в команде-источнике и открывает в новой.</summary>
    public async Task TransferPlayerAsync(
        Guid stageId,
        Guid playerId,
        Guid fromTeamId,
        Guid toTeamId,
        CancellationToken cancellationToken = default)
    {
        var stage = await GetStageOrThrowAsync(stageId, cancellationToken);
        EnsureRosterChangeable(stage);

        if (fromTeamId == toTeamId)
            throw new DomainException("Нельзя перевести игрока в ту же самую команду.");

        var from = await repository.GetByStageAndTeamAsync(stageId, fromTeamId, cancellationToken)
            ?? throw new NotFoundException("Команда-источник не заявлена на этот этап.");
        var to = await repository.GetByStageAndTeamAsync(stageId, toTeamId, cancellationToken)
            ?? throw new NotFoundException("Команда назначения не заявлена на этот этап.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        from.TransferOut(playerId, today);
        to.RegisterPlayer(playerId, today);

        // Оба агрегата в одном DbContext — сохраняются одной транзакцией.
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task WithdrawTeamAsync(
        Guid stageId,
        Guid tournamentTeamId,
        CancellationToken cancellationToken = default)
    {
        var stage = await GetStageOrThrowAsync(stageId, cancellationToken);
        EnsureRosterChangeable(stage);

        var tournamentTeam = await GetTeamOrThrowAsync(stageId, tournamentTeamId, cancellationToken);
        tournamentTeam.Withdraw(DateOnly.FromDateTime(DateTime.UtcNow));

        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Удаление ошибочной заявки игрока — только если по ней нет событий в матчах.
    /// Обычный уход из команды — отзаявка или трансфер: они сохраняют историю.
    /// </summary>
    public async Task DeleteRegistrationAsync(
        Guid stageId,
        Guid tournamentTeamId,
        Guid registrationId,
        CancellationToken cancellationToken = default)
    {
        var stage = await GetStageOrThrowAsync(stageId, cancellationToken);
        EnsureRosterChangeable(stage);

        var tournamentTeam = await GetTeamOrThrowAsync(stageId, tournamentTeamId, cancellationToken);
        var registration = tournamentTeam.Registrations.FirstOrDefault(item => item.Id == registrationId)
            ?? throw new NotFoundException($"Заявка '{registrationId}' не найдена в этой команде.");

        var hasEvents = false;
        foreach (var source in usageSources)
        {
            if (await source.PlayerHasEventsAsync(stageId, tournamentTeam.TeamId, registration.PlayerId, cancellationToken))
            {
                hasEvents = true;
                break;
            }
        }

        tournamentTeam.RemoveRegistration(registrationId, hasEvents);
        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Удаление ошибочной заявки команды вместе с заявками игроков — только если у команды
    /// нет матчей на этапе. Иначе команду снимают с этапа (история сохраняется).
    /// </summary>
    public async Task DeleteTeamAsync(
        Guid stageId,
        Guid tournamentTeamId,
        CancellationToken cancellationToken = default)
    {
        var stage = await GetStageOrThrowAsync(stageId, cancellationToken);
        EnsureRosterChangeable(stage);

        var tournamentTeam = await GetTeamOrThrowAsync(stageId, tournamentTeamId, cancellationToken);

        var matchesCount = 0;
        foreach (var source in usageSources)
            matchesCount += await source.CountTeamMatchesAsync(stageId, tournamentTeam.TeamId, cancellationToken);

        TournamentTeam.EnsureCanDelete(matchesCount);

        repository.Remove(tournamentTeam);
        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Заявлена ли команда на этап — используется при создании матча.</summary>
    public async Task<bool> IsTeamRegisteredAsync(
        Guid stageId,
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        await repository.GetByStageAndTeamAsync(stageId, teamId, cancellationToken)
            is { Status: TournamentTeamStatus.Active };

    private async Task<StageRosterInfo> GetStageOrThrowAsync(Guid stageId, CancellationToken cancellationToken) =>
        await stageInfo.GetAsync(stageId, cancellationToken)
        ?? throw new NotFoundException($"Этап '{stageId}' не найден.");

    private static void EnsureRosterChangeable(StageRosterInfo stage)
    {
        if (stage.RosterLocked)
            throw new ConflictException("Турнир завершён или отменён: заявки менять нельзя.");
    }

    private async Task<TournamentTeam> GetTeamOrThrowAsync(
        Guid stageId,
        Guid tournamentTeamId,
        CancellationToken cancellationToken)
    {
        var tournamentTeam = await repository.GetByIdAsync(tournamentTeamId, cancellationToken);

        if (tournamentTeam is null || tournamentTeam.StageId != stageId)
            throw new NotFoundException($"Заявка команды '{tournamentTeamId}' не найдена на этом этапе.");

        return tournamentTeam;
    }
}
