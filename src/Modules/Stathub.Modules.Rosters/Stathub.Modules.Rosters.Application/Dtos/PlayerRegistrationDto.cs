using Stathub.Modules.Rosters.Domain.Entities;
using Stathub.Modules.Rosters.Domain.Enums;

namespace Stathub.Modules.Rosters.Application.Dtos;

public sealed record PlayerRegistrationDto(
    Guid Id,
    Guid PlayerId,
    PlayerRegistrationStatus Status,
    DateOnly JoinedOn,
    DateOnly? LeftOn)
{
    internal static PlayerRegistrationDto From(PlayerRegistration registration) => new(
        registration.Id,
        registration.PlayerId,
        registration.Status,
        registration.JoinedOn,
        registration.LeftOn);
}
