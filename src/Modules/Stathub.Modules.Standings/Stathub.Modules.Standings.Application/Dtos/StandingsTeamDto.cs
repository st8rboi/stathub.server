namespace Stathub.Modules.Standings.Application.Dtos;

/// <summary>Команда в таблице: имя и логотип (витрина показывает гербы в строке).</summary>
public sealed record StandingsTeamDto(Guid Id, string Name, string? LogoUrl);
