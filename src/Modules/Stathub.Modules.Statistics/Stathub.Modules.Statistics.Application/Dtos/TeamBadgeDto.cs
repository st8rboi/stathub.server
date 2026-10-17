namespace Stathub.Modules.Statistics.Application.Dtos;

/// <summary>Команда для строки статистики: имя и логотип.</summary>
public sealed record TeamBadgeDto(Guid Id, string Name, string? LogoUrl);
