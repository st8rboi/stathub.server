using Stathub.Modules.Discipline.Domain.Entities;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Discipline.Application;

public sealed record DisqualificationDto(
    Guid Id,
    Guid TournamentId,
    Guid TeamId,
    string TeamName,
    Guid PlayerId,
    string PlayerName,
    string Reason,
    int? MatchesBanned,
    int MatchesServed,
    int? MatchesLeft,
    bool IsActive,
    DateTime IssuedAtUtc,
    DateTime? LiftedAtUtc,
    string? Comment);

/// <summary>Матчи команды в турнире — для подсчёта отбытого срока.</summary>
public interface ITeamMatchesSource
{
    /// <summary>
    /// Матчи команды в турнире, доигранные (по финальному свистку) строго после
    /// <paramref name="afterUtc"/> и до <paramref name="beforeUtc"/>, если граница задана.
    /// </summary>
    Task<int> CountFinishedBetweenAsync(
        Guid tournamentId,
        Guid teamId,
        DateTime afterUtc,
        DateTime? beforeUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>Заявка игрока за команду турнира: с какого и по какой день он за неё играл.</summary>
public sealed record PlayerTeamStint(Guid TeamId, DateOnly JoinedOn, DateOnly? LeftOn);

/// <summary>История заявок игрока — реализуется адаптером поверх модуля заявок.</summary>
public interface IPlayerRosterHistory
{
    /// <summary>Все заявки игрока в турнире, включая закрытые (отзаявка, трансфер).</summary>
    Task<IReadOnlyList<PlayerTeamStint>> GetStintsAsync(
        Guid tournamentId,
        Guid playerId,
        CancellationToken cancellationToken = default);
}

public interface IDisciplineDirectory
{
    Task<IReadOnlyDictionary<Guid, string>> GetTeamNamesAsync(
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetPlayerNamesAsync(
        IReadOnlyCollection<Guid> playerIds,
        CancellationToken cancellationToken = default);
}

public interface IDisqualificationRepository
{
    Task<Disqualification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Disqualification>> ListByTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    /// <summary>Не снятая дисквалификация игрока в турнире — независимо от команды.</summary>
    Task<Disqualification?> FindActiveAsync(
        Guid tournamentId,
        Guid playerId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Disqualification disqualification, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Дисциплинарный учёт: выдача, снятие, список и остаток срока.
/// <para>
/// Дисквалификация следует за игроком в пределах турнира: после трансфера она действует
/// и в новой команде. Срок отбывается матчами той команды, за которую игрок заявлен на момент
/// матча; в зачёт идут только матчи, доигранные после выдачи.
/// </para>
/// </summary>
public sealed class DisciplineService(
    IDisqualificationRepository repository,
    ITeamMatchesSource teamMatches,
    IPlayerRosterHistory rosterHistory,
    IDisciplineDirectory directory)
{
    public async Task<Guid> IssueAsync(
        Guid tournamentId,
        Guid teamId,
        Guid playerId,
        string reason,
        int? matchesBanned,
        Guid? issuedByUserId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var stints = await rosterHistory.GetStintsAsync(tournamentId, playerId, cancellationToken);
        if (stints.All(stint => stint.TeamId != teamId))
            throw new DomainException("Игрок не заявлен за эту команду в турнире.");

        var existing = await repository.FindActiveAsync(tournamentId, playerId, cancellationToken);
        if (existing is not null)
        {
            // Отбывание считается на чтении, поэтому отбытая запись в БД остаётся незакрытой.
            // Действующей она уже не является — закрываем её и выдаём новую; иначе после первой
            // отбытой дисквалификации игроку нельзя было бы выдать следующую.
            var served = existing.MatchesBanned is { } banned
                && await CountServedAsync(existing, stints, cancellationToken) >= banned;

            if (!served)
                throw new ConflictException("У игрока уже есть действующая дисквалификация в этом турнире.");

            existing.Lift();
        }

        var disqualification = Disqualification.Issue(
            tournamentId,
            teamId,
            playerId,
            reason,
            matchesBanned,
            issuedByUserId,
            comment);

        await repository.AddAsync(disqualification, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return disqualification.Id;
    }

    /// <summary>Турнир дисквалификации — для проверки владения: Id приходит из URL.</summary>
    public async Task<Guid> GetTournamentIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var disqualification = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Дисквалификация не найдена.");

        return disqualification.TournamentId;
    }

    public async Task LiftAsync(Guid id, string? comment, CancellationToken cancellationToken = default)
    {
        var disqualification = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Дисквалификация не найдена.");

        disqualification.Lift(comment);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DisqualificationDto>> ListAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        var list = await repository.ListByTournamentAsync(tournamentId, cancellationToken);
        if (list.Count == 0)
            return [];

        var teamNames = await directory.GetTeamNamesAsync(
            list.Select(item => item.TeamId).Distinct().ToList(),
            cancellationToken);
        var playerNames = await directory.GetPlayerNamesAsync(
            list.Select(item => item.PlayerId).Distinct().ToList(),
            cancellationToken);

        var result = new List<DisqualificationDto>(list.Count);

        foreach (var item in list)
        {
            var served = await CountServedAsync(item, cancellationToken);

            int? left = item.MatchesBanned is { } banned ? Math.Max(0, banned - served) : null;
            var active = item.IsActive && (left is null || left > 0);

            result.Add(new DisqualificationDto(
                item.Id,
                item.TournamentId,
                item.TeamId,
                teamNames.GetValueOrDefault(item.TeamId, string.Empty),
                item.PlayerId,
                playerNames.GetValueOrDefault(item.PlayerId, string.Empty),
                item.Reason,
                item.MatchesBanned,
                served,
                left,
                active,
                item.IssuedAtUtc,
                item.LiftedAtUtc,
                item.Comment));
        }

        return result.OrderByDescending(item => item.IssuedAtUtc).ToList();
    }

    /// <summary>
    /// Дисквалифицирован ли игрок сейчас (для пометок в протоколе). Команда не важна:
    /// бан следует за игроком и после трансфера.
    /// </summary>
    public async Task<int?> GetBanLeftAsync(
        Guid tournamentId,
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var active = await repository.FindActiveAsync(tournamentId, playerId, cancellationToken);
        if (active is null)
            return null;

        if (active.MatchesBanned is not { } banned)
            return int.MaxValue;

        var left = Math.Max(0, banned - await CountServedAsync(active, cancellationToken));

        return left > 0 ? left : null;
    }

    private async Task<int> CountServedAsync(Disqualification item, CancellationToken cancellationToken) =>
        await CountServedAsync(
            item,
            await rosterHistory.GetStintsAsync(item.TournamentId, item.PlayerId, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Отбытые матчи: по каждой заявке игрока — матчи её команды, доигранные после выдачи и
    /// пока заявка действовала. Без истории заявок (старые записи) — матчи команды из дисквалификации.
    /// </summary>
    private async Task<int> CountServedAsync(
        Disqualification item,
        IReadOnlyList<PlayerTeamStint> stints,
        CancellationToken cancellationToken)
    {
        if (stints.Count == 0)
            return await teamMatches.CountFinishedBetweenAsync(
                item.TournamentId, item.TeamId, item.IssuedAtUtc, beforeUtc: null, cancellationToken);

        var served = 0;

        foreach (var stint in stints)
        {
            var joinedAtUtc = stint.JoinedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var afterUtc = joinedAtUtc > item.IssuedAtUtc ? joinedAtUtc : item.IssuedAtUtc;

            // День ухода уже не день старой команды: при трансфере он же — первый день в новой.
            DateTime? beforeUtc = stint.LeftOn?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            if (beforeUtc is { } before && before <= afterUtc)
                continue;

            served += await teamMatches.CountFinishedBetweenAsync(
                item.TournamentId, stint.TeamId, afterUtc, beforeUtc, cancellationToken);
        }

        return served;
    }
}
