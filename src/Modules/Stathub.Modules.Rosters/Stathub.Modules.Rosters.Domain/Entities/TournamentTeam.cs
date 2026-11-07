using Stathub.Modules.Rosters.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Rosters.Domain.Entities;

/// <summary>
/// Заявка команды на этап турнира (группа, дивизион, плей-офф). Именно этап, а не турнир:
/// в одном турнире бывают параллельные дивизионы, и в каждом играют свои команды.
/// <para>
/// Турнир хранится рядом как контекст: по нему проверяется блокировка заявок на завершённый
/// турнир. Уникальность игрока ограничена этапом: в разных дивизионах одного турнира
/// он может играть за разные команды.
/// </para>
/// </summary>
public sealed class TournamentTeam : AggregateRoot
{
    private readonly List<PlayerRegistration> _registrations = [];

    public Guid StageId { get; private set; }

    public Guid TournamentId { get; private set; }

    public Guid TeamId { get; private set; }

    public TournamentTeamStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<PlayerRegistration> Registrations => _registrations.AsReadOnly();

    private TournamentTeam()
    {
    }

    private TournamentTeam(Guid id, Guid stageId, Guid tournamentId, Guid teamId)
    {
        Id = id;
        StageId = stageId;
        TournamentId = tournamentId;
        TeamId = teamId;
        Status = TournamentTeamStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static TournamentTeam Create(Guid stageId, Guid tournamentId, Guid teamId)
    {
        if (stageId == Guid.Empty)
            throw new DomainException("StageId обязательное поле.");
        if (tournamentId == Guid.Empty)
            throw new DomainException("TournamentId обязательное поле.");
        if (teamId == Guid.Empty)
            throw new DomainException("TeamId обязательное поле.");

        return new TournamentTeam(Guid.NewGuid(), stageId, tournamentId, teamId);
    }

    public PlayerRegistration RegisterPlayer(Guid playerId, int? squadNumber, DateOnly joinedOn)
    {
        if (Status != TournamentTeamStatus.Active)
            throw new ConflictException("Команда снята с этапа: заявить игрока нельзя.");
        if (playerId == Guid.Empty)
            throw new DomainException("PlayerId обязательное поле.");

        if (_registrations.Any(registration =>
                registration.PlayerId == playerId && registration.Status == PlayerRegistrationStatus.Active))
        {
            throw new ConflictException("Игрок уже заявлен за эту команду.");
        }

        if (squadNumber is not null)
        {
            if (squadNumber is < 1 or > 999)
                throw new DomainException("Игровой номер должен быть от 1 до 999.");

            if (_registrations.Any(registration =>
                    registration.SquadNumber == squadNumber && registration.Status == PlayerRegistrationStatus.Active))
            {
                throw new ConflictException($"Игровой номер {squadNumber} уже занят в этой команде.");
            }
        }

        var registration = PlayerRegistration.Create(Id, playerId, squadNumber, joinedOn);
        _registrations.Add(registration);
        return registration;
    }

    /// <summary>Закрывает активную заявку игрока статусом «перешёл в другую команду».</summary>
    public PlayerRegistration TransferOut(Guid playerId, DateOnly date)
    {
        var registration = _registrations.FirstOrDefault(item =>
            item.PlayerId == playerId && item.Status == PlayerRegistrationStatus.Active)
            ?? throw new NotFoundException("У игрока нет активной заявки в этой команде.");

        registration.Close(PlayerRegistrationStatus.TransferredOut, date);
        return registration;
    }

    public PlayerRegistration ReleasePlayer(Guid registrationId, DateOnly date)
    {
        var registration = _registrations.FirstOrDefault(item => item.Id == registrationId)
            ?? throw new NotFoundException($"Заявка '{registrationId}' не найдена в этой команде.");

        if (registration.Status != PlayerRegistrationStatus.Active)
            throw new ConflictException("Заявка уже закрыта.");

        registration.Close(PlayerRegistrationStatus.Released, date);
        return registration;
    }

    /// <summary>Снятие команды с этапа: все активные заявки закрываются, история остаётся.</summary>
    public void Withdraw(DateOnly date)
    {
        if (Status != TournamentTeamStatus.Active)
            throw new ConflictException("Команда уже снята с этапа.");

        Status = TournamentTeamStatus.Withdrawn;

        foreach (var registration in _registrations.Where(item => item.Status == PlayerRegistrationStatus.Active))
            registration.Close(PlayerRegistrationStatus.Released, date);
    }
}
