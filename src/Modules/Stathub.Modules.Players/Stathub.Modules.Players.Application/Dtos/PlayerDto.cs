using Stathub.Modules.Players.Domain.Entities;

namespace Stathub.Modules.Players.Application.Dtos;

public sealed record PlayerDto(
    Guid Id,
    string FullName,
    DateOnly? BirthDate,
    string? PhotoUrl,
    Guid? PhotoOwnerTenantId,
    DateTime CreatedAtUtc)
{
    internal static PlayerDto From(Player player) => new(
        player.Id,
        player.FullName,
        player.BirthDate,
        player.PhotoUrl,
        player.PhotoOwnerTenantId,
        player.CreatedAtUtc);
}
