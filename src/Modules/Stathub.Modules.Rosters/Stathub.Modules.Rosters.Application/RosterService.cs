using Stathub.Modules.Rosters.Application.Dtos;
using Stathub.Modules.Rosters.Application.Interfaces;
using Stathub.Modules.Rosters.Domain.Entities;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Rosters.Application;

/// <summary>
/// Заявки команд и игроков на турнир (сезон).
///
/// <para>
/// Модуль ссылается на турнир, команду и игрока только по <c>Guid</c> и проверяет их существование
/// через читатели модулей-поставщиков: межмодульные FK и прямые ссылки на чужие домены запрещены
/// (AGENTS.md).
/// </para>
/// <para>
/// Правила: одна команда — одна заявка на турнир; игрок — не больше одной активной заявки в турнире;
/// игровой номер уникален среди активных в команде; менять заявки можно, пока турнир не завершён.
/// </para>
/// </summary>
public sealed class RosterService(
    ITournamentTeamRepository repository,
    ITournamentInfoProvider tournamentInfo,
    ITeamExistsProvider teamExists,
    IPlayerExistsProvider playerExists)
{
    public async Task<Guid> AddTeamAsync(
        Guid tournamentId,
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRosterChangeableAsync(tournamentId, cancellationToken);

        if (!await teamExists.ExistsAsync(teamId, cancellationToken))
            throw new NotFoundException($"Команда '{teamId}' не найдена.");

        if (await repository.GetByTournamentAndTeamAsync(tournamentId, teamId, cancellationToken) is not null)
            throw new ConflictException("Команда уже заявлена в этом турнире.");

        var tournamentTeam = TournamentTeam.Create(tournamentId, teamId);

        await repository.AddAsync(tournamentTeam, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return tournamentTeam.Id;
    }

    public async Task<IReadOnlyList<TournamentTeamDto>> ListTeamsAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        var teams = await repository.ListByTournamentAsync(tournamentId, cancellationToken);
        return teams.Select(TournamentTeamDto.From).ToList();
    }

    public async Task<TournamentTeamDetailsDto> GetTeamAsync(
        Guid tournamentId,
        Guid tournamentTeamId,
        CancellationToken cancellationToken = default)
    {
        var tournamentTeam = await GetTeamOrThrowAsync(tournamentId, tournamentTeamId, cancellationToken);
        return TournamentTeamDetailsDto.From(tournamentTeam);
    }

    public async Task<Guid> RegisterPlayerAsync(
        Guid tournamentId,
        Guid tournamentTeamId,
        Guid playerId,
        int? squadNumber,
        CancellationToken cancellationToken = default)
    {
        await EnsureRosterChangeableAsync(tournamentId, cancellationToken);

        var tournamentTeam = await GetTeamOrThrowAsync(tournamentId, tournamentTeamId, cancellationToken);

        if (!await playerExists.ExistsAsync(playerId, cancellationToken))
            throw new NotFoundException($"Игрок '{playerId}' не найден.");

        if (await repository.FindTeamIdWithActiveRegistrationAsync(tournamentId, playerId, cancellationToken) is not null)
            throw new ConflictException(
                "Игрок уже заявлен за другую команду этого турнира — оформите трансфер вместо новой заявки.");

        var registration = tournamentTeam.RegisterPlayer(
            playerId,
            squadNumber,
            DateOnly.FromDateTime(DateTime.UtcNow));

        await repository.SaveChangesAsync(cancellationToken);

        return registration.Id;
    }

    /// <summary>Отзаявка игрока: заявка закрывается, но остаётся в истории.</summary>
    public async Task ReleasePlayerAsync(
        Guid tournamentId,
        Guid tournamentTeamId,
        Guid registrationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRosterChangeableAsync(tournamentId, cancellationToken);

        var tournamentTeam = await GetTeamOrThrowAsync(tournamentId, tournamentTeamId, cancellationToken);
        tournamentTeam.ReleasePlayer(registrationId, DateOnly.FromDateTime(DateTime.UtcNow));

        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Трансфер: закрывает заявку игрока в команде-источнике и открывает новую в команде назначения
    /// того же турнира. Обе записи остаются в истории.
    /// </summary>
    public async Task TransferPlayerAsync(
        Guid tournamentId,
        Guid playerId,
        Guid fromTeamId,
        Guid toTeamId,
        int? squadNumber = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureRosterChangeableAsync(tournamentId, cancellationToken);

        if (fromTeamId == toTeamId)
            throw new DomainException("Нельзя перевести игрока в ту же самую команду.");

        var from = await repository.GetByTournamentAndTeamAsync(tournamentId, fromTeamId, cancellationToken)
            ?? throw new NotFoundException("Команда-источник не заявлена в этом турнире.");
        var to = await repository.GetByTournamentAndTeamAsync(tournamentId, toTeamId, cancellationToken)
            ?? throw new NotFoundException("Команда назначения не заявлена в этом турнире.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        from.TransferOut(playerId, today);
        to.RegisterPlayer(playerId, squadNumber, today);

        // Оба агрегата в одном DbContext — сохраняются одной транзакцией.
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task WithdrawTeamAsync(
        Guid tournamentId,
        Guid tournamentTeamId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRosterChangeableAsync(tournamentId, cancellationToken);

        var tournamentTeam = await GetTeamOrThrowAsync(tournamentId, tournamentTeamId, cancellationToken);
        tournamentTeam.Withdraw(DateOnly.FromDateTime(DateTime.UtcNow));

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureRosterChangeableAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await tournamentInfo.GetAsync(tournamentId, cancellationToken)
            ?? throw new NotFoundException($"Турнир '{tournamentId}' не найден.");

        if (tournament.RosterLocked)
            throw new ConflictException("Турнир завершён или отменён: заявки менять нельзя.");
    }

    private async Task<TournamentTeam> GetTeamOrThrowAsync(
        Guid tournamentId,
        Guid tournamentTeamId,
        CancellationToken cancellationToken)
    {
        var tournamentTeam = await repository.GetByIdAsync(tournamentTeamId, cancellationToken);

        if (tournamentTeam is null || tournamentTeam.TournamentId != tournamentId)
            throw new NotFoundException($"Заявка команды '{tournamentTeamId}' не найдена в этом турнире.");

        return tournamentTeam;
    }
}
