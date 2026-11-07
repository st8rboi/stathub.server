namespace Stathub.Modules.Players.Application.Dtos;

/// <summary>ФИО игрока для витрин и статистики: потребителю не нужны остальные поля.</summary>
public sealed record PlayerNameDto(Guid Id, string FullName);
