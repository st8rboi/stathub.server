using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Shared.Domain;

namespace Stathub.WebApi.Contracts.Leagues;

public sealed record CreateLeagueRequest(
    Guid OrganizerId,
    string Name,
    string Slug,
    Sport Sport,
    string? City,
    string? Region,
    LeagueDataSource DataSource = LeagueDataSource.Native);
