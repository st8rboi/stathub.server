using Stathub.Modules.Rosters.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Rosters.Domain.Entities;

/// <summary>
/// Заявка команды на турнир (сезон). Одна команда — одна заявка на турнир.
/// Состав (игроки) живёт внутри этой заявки, поэтому трансфер — это закрытие старой заявки
/// игрока и открытие новой в другой команде: история матчей и событий не рвётся (§7).
/// </summary>
public sealed class TournamentTeam : AggregateRoot
{
    private readonly List<PlayerRegistration> _registrations = [];

    public Guid TournamentId { get; private set; }
    public Guid TeamId { get; private set; }
    public TournamentTeamStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<PlayerRegistration> Registrations => _registrations.AsReadOnly();

    private TournamentTeam()
    {
    }

    private TournamentTeam(Guid id, Guid tournamentId, Guid teamId)
    {
        Id = id;
        TournamentId = tournamentId;
        TeamId = teamId;
        Status = TournamentTeamStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static TournamentTeam Create(Guid tournamentId, Guid teamId)
    {
        if (tournamentId == Guid.Empty)
            throw new DomainException("TournamentId обязательное поле.");
        if (teamId == Guid.Empty)
            throw new DomainException("TeamId обязательное поле.");

        return new TournamentTeam(Guid.NewGuid(), tournamentId, teamId);
    }

    public PlayerRegistration RegisterPlayer(Guid playerId, int? squadNumber, DateOnly joinedOn)
    {
        if (Status != TournamentTeamStatus.Active)
            throw new ConflictException("Команда снята с турнира: заявить игрока нельзя.");
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

    /// <summary>Снятие команды с турнира: все активные заявки закрываются, история остаётся.</summary>
    public void Withdraw(DateOnly date)
    {
        if (Status != TournamentTeamStatus.Active)
            throw new ConflictException("Команда уже снята с турнира.");

        Status = TournamentTeamStatus.Withdrawn;

        foreach (var registration in _registrations.Where(item => item.Status == PlayerRegistrationStatus.Active))
            registration.Close(PlayerRegistrationStatus.Released, date);
    }
}
