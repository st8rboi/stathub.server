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
    /// <summary>Завершённые матчи команды в турнире с указанного момента.</summary>
    Task<int> CountFinishedSinceAsync(
        Guid tournamentId,
        Guid teamId,
        DateTime sinceUtc,
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

    Task<Disqualification?> FindActiveAsync(
        Guid tournamentId,
        Guid teamId,
        Guid playerId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Disqualification disqualification, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Дисциплинарный учёт: выдача, снятие, список и остаток срока.</summary>
public sealed class DisciplineService(
    IDisqualificationRepository repository,
    ITeamMatchesSource teamMatches,
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
        var existing = await repository.FindActiveAsync(tournamentId, teamId, playerId, cancellationToken);
        if (existing is not null)
        {
            // Отбывание считается на чтении, поэтому отбытая запись в БД остаётся незакрытой.
            // Действующей она уже не является — закрываем её и выдаём новую; иначе после первой
            // отбытой дисквалификации игроку нельзя было бы выдать следующую.
            var served = existing.MatchesBanned is { } banned
                && await teamMatches.CountFinishedSinceAsync(tournamentId, teamId, existing.IssuedAtUtc, cancellationToken) >= banned;

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
            var served = await teamMatches.CountFinishedSinceAsync(
                item.TournamentId, item.TeamId, item.IssuedAtUtc, cancellationToken);

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

    /// <summary>Дисквалифицирован ли игрок на момент времени (для блокировки событий).</summary>
    public async Task<int?> GetBanLeftAsync(
        Guid tournamentId,
        Guid teamId,
        Guid playerId,
        DateTime atUtc,
        CancellationToken cancellationToken = default)
    {
        var active = await repository.FindActiveAsync(tournamentId, teamId, playerId, cancellationToken);
        if (active is null)
            return null;

        if (active.MatchesBanned is not { } banned)
            return int.MaxValue;

        var served = await teamMatches.CountFinishedSinceAsync(tournamentId, teamId, active.IssuedAtUtc, cancellationToken);
        var left = Math.Max(0, banned - served);

        return left > 0 ? left : null;
    }
}
