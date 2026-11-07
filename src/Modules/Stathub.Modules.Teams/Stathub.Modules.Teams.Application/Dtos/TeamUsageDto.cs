namespace Stathub.Modules.Teams.Application.Dtos;

/// <summary>
/// Использование команды в одном модуле-потребителе.
/// </summary>
/// <param name="Description">Человекочитаемо для ошибки, например «заявки: 2».</param>
/// <param name="OrganizerIds">Организаторы, в турнирах которых есть ссылки на команду.</param>
public sealed record TeamUsageDto(string Description, IReadOnlyCollection<Guid> OrganizerIds);
