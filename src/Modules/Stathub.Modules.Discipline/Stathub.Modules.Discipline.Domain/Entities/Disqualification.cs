using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Discipline.Domain.Entities;

/// <summary>
/// Дисквалификация игрока в пределах турнира. Срок задаётся числом матчей команды
/// (null — бессрочно до снятия организатором). Отбывание считается на чтении:
/// завершённые матчи этой команды после выдачи уменьшают остаток.
/// </summary>
public sealed class Disqualification : AggregateRoot
{
    public Guid TournamentId { get; private set; }

    public Guid TeamId { get; private set; }

    public Guid PlayerId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>Сколько матчей команды нужно пропустить; null — бессрочно.</summary>
    public int? MatchesBanned { get; private set; }

    public DateTime IssuedAtUtc { get; private set; }

    public Guid? IssuedByUserId { get; private set; }

    public DateTime? LiftedAtUtc { get; private set; }

    public string? Comment { get; private set; }

    private Disqualification()
    {
    }

    private Disqualification(
        Guid id,
        Guid tournamentId,
        Guid teamId,
        Guid playerId,
        string reason,
        int? matchesBanned,
        Guid? issuedByUserId,
        string? comment)
    {
        Id = id;
        TournamentId = tournamentId;
        TeamId = teamId;
        PlayerId = playerId;
        Reason = reason;
        MatchesBanned = matchesBanned;
        IssuedByUserId = issuedByUserId;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        IssuedAtUtc = DateTime.UtcNow;
    }

    public static Disqualification Issue(
        Guid tournamentId,
        Guid teamId,
        Guid playerId,
        string reason,
        int? matchesBanned,
        Guid? issuedByUserId = null,
        string? comment = null)
    {
        if (tournamentId == Guid.Empty)
            throw new DomainException("Турнир обязателен.");
        if (teamId == Guid.Empty)
            throw new DomainException("Команда обязательна.");
        if (playerId == Guid.Empty)
            throw new DomainException("Игрок обязателен.");

        var normalizedReason = (reason ?? string.Empty).Trim();
        if (normalizedReason.Length == 0)
            throw new DomainException("Причина дисквалификации обязательна.");
        if (normalizedReason.Length > 500)
            throw new DomainException("Причина не может быть длиннее 500 символов.");

        if (matchesBanned is <= 0)
            throw new DomainException("Срок должен быть положительным числом матчей или пустым (бессрочно).");
        if (matchesBanned is > 100)
            throw new DomainException("Срок больше 100 матчей — проверьте значение.");

        return new Disqualification(
            Guid.NewGuid(),
            tournamentId,
            teamId,
            playerId,
            normalizedReason,
            matchesBanned,
            issuedByUserId,
            comment);
    }

    public void Lift(string? comment = null)
    {
        if (LiftedAtUtc is not null)
            throw new ConflictException("Дисквалификация уже снята.");

        LiftedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(comment))
            Comment = comment.Trim();
    }

    public bool IsActive => LiftedAtUtc is null;
}
