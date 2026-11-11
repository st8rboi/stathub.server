using Stathub.Modules.Rosters.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Rosters.Domain.Entities;

/// <summary>
/// Заявка игрока за команду в турнире. Существует только внутри <see cref="TournamentTeam"/>.
/// Статус живёт здесь, а не у игрока: он меняется от сезона к сезону (§7).
/// </summary>
public sealed class PlayerRegistration : Entity
{
    public Guid TournamentTeamId { get; private set; }
    public Guid PlayerId { get; private set; }
    public PlayerRegistrationStatus Status { get; private set; }
    public DateOnly JoinedOn { get; private set; }
    public DateOnly? LeftOn { get; private set; }

    private PlayerRegistration()
    {
    }

    private PlayerRegistration(
        Guid id,
        Guid tournamentTeamId,
        Guid playerId,
        DateOnly joinedOn)
    {
        Id = id;
        TournamentTeamId = tournamentTeamId;
        PlayerId = playerId;
        Status = PlayerRegistrationStatus.Active;
        JoinedOn = joinedOn;
    }

    internal static PlayerRegistration Create(
        Guid tournamentTeamId,
        Guid playerId,
        DateOnly joinedOn) =>
        new(Guid.NewGuid(), tournamentTeamId, playerId, joinedOn);

    internal void Close(PlayerRegistrationStatus status, DateOnly date)
    {
        if (status == PlayerRegistrationStatus.Active)
            throw new DomainException("Закрытие заявки требует статуса «перешёл» или «отзаявлен».");
        if (Status != PlayerRegistrationStatus.Active)
            throw new ConflictException("Заявка уже закрыта.");

        Status = status;
        LeftOn = date;
    }
}
