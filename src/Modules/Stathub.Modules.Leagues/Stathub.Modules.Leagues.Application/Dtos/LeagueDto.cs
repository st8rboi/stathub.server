using Stathub.Modules.Leagues.Domain.Entities;
using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Shared.Domain;

namespace Stathub.Modules.Leagues.Application.Dtos;

public sealed record LeagueDto(
    Guid Id,
    Guid OrganizerId,
    string Name,
    string Slug,
    Sport Sport,
    string? City,
    string? Region,
    LeagueDataSource DataSource,
    LeagueStatus Status,
    DateTime CreatedAtUtc)
{
    internal static LeagueDto From(League league) => new(
        league.Id,
        league.OrganizerId,
        league.Name,
        league.Slug,
        league.Sport,
        league.City,
        league.Region,
        league.DataSource,
        league.Status,
        league.CreatedAtUtc);
}
