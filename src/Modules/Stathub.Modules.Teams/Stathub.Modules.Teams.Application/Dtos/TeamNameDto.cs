namespace Stathub.Modules.Teams.Application.Dtos;

/// <summary>Имя и логотип команды — минимальный контракт для таблиц и витрины.</summary>
public sealed record TeamNameDto(Guid Id, string Name, string? LogoUrl);
