namespace Stathub.Modules.Players.Application.Dtos;

/// <summary>
/// Использование игрока в одном модуле-потребителе.
/// </summary>
/// <param name="Description">Человекочитаемо для ошибки, например «заявки: 2».</param>
/// <param name="OrganizerIds">Организаторы, в турнирах которых есть ссылки на игрока.</param>
public sealed record PlayerUsageDto(string Description, IReadOnlyCollection<Guid> OrganizerIds);
