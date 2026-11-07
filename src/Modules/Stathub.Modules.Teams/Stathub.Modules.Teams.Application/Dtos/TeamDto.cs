using Stathub.Modules.Teams.Domain.Entities;

namespace Stathub.Modules.Teams.Application.Dtos;

public sealed record TeamDto(
    Guid Id,
    string Name,
    string? City,
    string? LogoUrl,
    Guid? LogoOwnerTenantId,
    DateTime CreatedAtUtc)
{
    internal static TeamDto From(Team team) => new(
        team.Id,
        team.Name,
        team.City,
        team.LogoUrl,
        team.LogoOwnerTenantId,
        team.CreatedAtUtc);
}
