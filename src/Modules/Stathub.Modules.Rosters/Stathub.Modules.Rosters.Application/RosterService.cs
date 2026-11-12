using Stathub.Modules.Rosters.Application.Dtos;
using Stathub.Modules.Rosters.Application.Interfaces;
using Stathub.Modules.Rosters.Domain.Entities;
using Stathub.Modules.Rosters.Domain.Enums;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Rosters.Application;

/// <summary>
/// Заявки команд и игроков в дивизион турнира.
/// <para>
/// Модуль ссылается на дивизион, турнир, команду и игрока только по <c>Guid</c> и проверяет
/// их существование через читатели модулей-поставщиков: межмодульные FK запрещены (AGENTS.md).
/// </para>
/// <para>
/// Правила: команда играет в одном дивизионе турнира; состав один на все этапы дивизиона;
/// игрок — не больше одной активной заявки на весь турнир (переход — трансфером, в том числе
/// между дивизионами); менять заявки можно, пока турнир не завершён.
/// </para>
/// </summary>
public sealed class RosterService(
    ITournamentTeamRepository repository,
    IDivisionInfoProvider divisionInfo,
    ITeamExistsProvider teamExists,
    IPlayerExistsProvider playerExists,
    IEnumerable<IRosterUsageSource> usageSources)
{
    public async Task<Guid> AddTeamAsync(
        Guid divisionId,
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var division = await GetDivisionOrThrowAsync(divisionId, cancellationToken);
        EnsureRosterChangeable(division);

        if (!await teamExists.ExistsAsync(teamId, cancellationToken))
            throw new NotFoundException($"Команда '{teamId}' не найдена.");

        if (await repository.GetByTournamentAndTeamAsync(division.TournamentId, teamId, cancellationToken) is { } existing)
            throw new ConflictException(existing.DivisionId == divisionId
                ? "Команда уже заявлена в этот дивизион."
                : "Команда уже заявлена в другой дивизион этого турнира.");

        var tournamentTeam = TournamentTeam.Create(divisionId, division.TournamentId, teamId);

        await repository.AddAsync(tournamentTeam, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return tournamentTeam.Id;
    }

    public async Task<IReadOnlyList<TournamentTeamDto>> ListTeamsAsync(
        Guid divisionId,
        CancellationToken cancellationToken = default)
    {
        var teams = await repository.ListByDivisionAsync(divisionId, cancellationToken);
        return teams.Select(TournamentTeamDto.From).ToList();
    }

    /// <summary>
    /// Дивизион, в котором проходит этап. Состав матча клиенты ищут по этапу матча,
    /// а заявки хранятся на дивизионе.
    /// </summary>
    public async Task<Guid> GetDivisionIdByStageAsync(Guid stageId, CancellationToken cancellationToken = default)
    {
        var division = await divisionInfo.GetByStageAsync(stageId, cancellationToken)
            ?? throw new NotFoundException($"Этап '{stageId}' не найден.");

        return division.DivisionId;
    }

    /// <summary>
    /// Все заявки команды с составами, новые первыми — публичная страница команды.
    /// Видимость (опубликована ли лига) решает вызывающий: модуль о публикации не знает.
    /// </summary>
    public async Task<IReadOnlyList<TournamentTeamDetailsDto>> ListByTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var entries = await repository.ListByTeamAsync(teamId, cancellationToken);
        return entries.Select(TournamentTeamDetailsDto.From).ToList();
    }

    public async Task<TournamentTeamDetailsDto> GetTeamAsync(
        Guid divisionId,
        Guid tournamentTeamId,
        CancellationToken cancellationToken = default)
    {
        var tournamentTeam = await GetTeamOrThrowAsync(divisionId, tournamentTeamId, cancellationToken);
        return TournamentTeamDetailsDto.From(tournamentTeam);
    }

    public async Task<Guid> RegisterPlayerAsync(
        Guid divisionId,
        Guid tournamentTeamId,
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var division = await GetDivisionOrThrowAsync(divisionId, cancellationToken);
        EnsureRosterChangeable(division);

        var tournamentTeam = await GetTeamOrThrowAsync(divisionId, tournamentTeamId, cancellationToken);

        if (!await playerExists.ExistsAsync(playerId, cancellationToken))
            throw new NotFoundException($"Игрок '{playerId}' не найден.");

        var currentTeamEntryId = await repository.FindTeamIdWithActiveRegistrationAsync(
            division.TournamentId, playerId, cancellationToken);

        // Своя команда отвечает точнее («уже заявлен за эту команду») — её проверит агрегат.
        if (currentTeamEntryId is { } entryId && entryId != tournamentTeam.Id)
            throw new ConflictException(
                "Игрок уже заявлен за другую команду этого турнира — оформите трансфер вместо новой заявки.");

        var registration = tournamentTeam.RegisterPlayer(
            playerId,
            DateOnly.FromDateTime(DateTime.UtcNow));

        await repository.SaveChangesAsync(cancellationToken);

        return registration.Id;
    }

    /// <summary>Отзаявка игрока: заявка закрывается, но остаётся в истории.</summary>
    public async Task ReleasePlayerAsync(
        Guid divisionId,
        Guid tournamentTeamId,
        Guid registrationId,
        CancellationToken cancellationToken = default)
    {
        var division = await GetDivisionOrThrowAsync(divisionId, cancellationToken);
        EnsureRosterChangeable(division);

        var tournamentTeam = await GetTeamOrThrowAsync(divisionId, tournamentTeamId, cancellationToken);
        tournamentTeam.ReleasePlayer(registrationId, DateOnly.FromDateTime(DateTime.UtcNow));

        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Трансфер внутри турнира: закрывает заявку в команде-источнике и открывает в новой.
    /// Команды могут быть из разных дивизионов турнира — игрок один на весь турнир.
    /// </summary>
    public async Task TransferPlayerAsync(
        Guid divisionId,
        Guid playerId,
        Guid fromTeamId,
        Guid toTeamId,
        CancellationToken cancellationToken = default)
    {
        var division = await GetDivisionOrThrowAsync(divisionId, cancellationToken);
        EnsureRosterChangeable(division);

        if (fromTeamId == toTeamId)
            throw new DomainException("Нельзя перевести игрока в ту же самую команду.");

        var from = await repository.GetByTournamentAndTeamAsync(division.TournamentId, fromTeamId, cancellationToken)
            ?? throw new NotFoundException("Команда-источник не заявлена на этот турнир.");
        var to = await repository.GetByTournamentAndTeamAsync(division.TournamentId, toTeamId, cancellationToken)
            ?? throw new NotFoundException("Команда назначения не заявлена на этот турнир.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        from.TransferOut(playerId, today);
        to.RegisterPlayer(playerId, today);

        // Оба агрегата в одном DbContext — сохраняются одной транзакцией.
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task WithdrawTeamAsync(
        Guid divisionId,
        Guid tournamentTeamId,
        CancellationToken cancellationToken = default)
    {
        var division = await GetDivisionOrThrowAsync(divisionId, cancellationToken);
        EnsureRosterChangeable(division);

        var tournamentTeam = await GetTeamOrThrowAsync(divisionId, tournamentTeamId, cancellationToken);
        tournamentTeam.Withdraw(DateOnly.FromDateTime(DateTime.UtcNow));

        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Удаление ошибочной заявки игрока — только если по ней нет событий в матчах.
    /// Обычный уход из команды — отзаявка или трансфер: они сохраняют историю.
    /// </summary>
    public async Task DeleteRegistrationAsync(
        Guid divisionId,
        Guid tournamentTeamId,
        Guid registrationId,
        CancellationToken cancellationToken = default)
    {
        var division = await GetDivisionOrThrowAsync(divisionId, cancellationToken);
        EnsureRosterChangeable(division);

        var tournamentTeam = await GetTeamOrThrowAsync(divisionId, tournamentTeamId, cancellationToken);
        var registration = tournamentTeam.Registrations.FirstOrDefault(item => item.Id == registrationId)
            ?? throw new NotFoundException($"Заявка '{registrationId}' не найдена в этой команде.");

        var hasEvents = false;
        foreach (var source in usageSources)
        {
            if (await source.PlayerHasEventsAsync(divisionId, tournamentTeam.TeamId, registration.PlayerId, cancellationToken))
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
    /// нет матчей в дивизионе. Иначе команду снимают с турнира (история сохраняется).
    /// </summary>
    public async Task DeleteTeamAsync(
        Guid divisionId,
        Guid tournamentTeamId,
        CancellationToken cancellationToken = default)
    {
        var division = await GetDivisionOrThrowAsync(divisionId, cancellationToken);
        EnsureRosterChangeable(division);

        var tournamentTeam = await GetTeamOrThrowAsync(divisionId, tournamentTeamId, cancellationToken);

        var matchesCount = 0;
        foreach (var source in usageSources)
            matchesCount += await source.CountTeamMatchesAsync(divisionId, tournamentTeam.TeamId, cancellationToken);

        TournamentTeam.EnsureCanDelete(matchesCount);

        repository.Remove(tournamentTeam);
        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Заявлена ли команда в дивизион — используется при создании матча.</summary>
    public async Task<bool> IsTeamRegisteredAsync(
        Guid divisionId,
        Guid teamId,
        CancellationToken cancellationToken = default) =>
        await repository.GetByDivisionAndTeamAsync(divisionId, teamId, cancellationToken)
            is { Status: TournamentTeamStatus.Active };

    private async Task<DivisionRosterInfo> GetDivisionOrThrowAsync(Guid divisionId, CancellationToken cancellationToken) =>
        await divisionInfo.GetAsync(divisionId, cancellationToken)
        ?? throw new NotFoundException($"Дивизион '{divisionId}' не найден.");

    private static void EnsureRosterChangeable(DivisionRosterInfo division)
    {
        if (division.RosterLocked)
            throw new ConflictException("Турнир завершён или отменён: заявки менять нельзя.");
    }

    private async Task<TournamentTeam> GetTeamOrThrowAsync(
        Guid divisionId,
        Guid tournamentTeamId,
        CancellationToken cancellationToken)
    {
        var tournamentTeam = await repository.GetByIdAsync(tournamentTeamId, cancellationToken);

        if (tournamentTeam is null || tournamentTeam.DivisionId != divisionId)
            throw new NotFoundException($"Заявка команды '{tournamentTeamId}' не найдена в этом дивизионе.");

        return tournamentTeam;
    }
}
