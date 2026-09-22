using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Shared.Domain;

namespace Stathub.WebApi.Contracts.Leagues;

/// <summary>
/// Запрос на создание новой лиги.
/// </summary>
/// <param name="OrganizerId"></param>
/// <param name="Name"></param>
/// <param name="Slug"></param>
/// <param name="Sport"></param>
/// <param name="City"></param>
/// <param name="Region"></param>
/// <param name="DataSource"></param>
public sealed record CreateLeagueRequest(
    Guid OrganizerId,
    string Name,
    string Slug,
    Sport Sport,
    string? City,
    string? Region,
    LeagueDataSource DataSource = LeagueDataSource.Native);
