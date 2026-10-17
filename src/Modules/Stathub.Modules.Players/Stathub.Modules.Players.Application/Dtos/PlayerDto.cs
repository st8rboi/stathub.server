using Stathub.Modules.Players.Domain.Entities;

namespace Stathub.Modules.Players.Application.Dtos;

public sealed record PlayerDto(
    Guid Id,
    string FullName,
    DateOnly? BirthDate,
    DateTime CreatedAtUtc)
{
    internal static PlayerDto From(Player player) => new(
        player.Id,
        player.FullName,
        player.BirthDate,
        player.CreatedAtUtc);
}
