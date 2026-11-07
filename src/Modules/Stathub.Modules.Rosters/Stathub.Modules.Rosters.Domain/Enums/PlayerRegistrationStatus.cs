namespace Stathub.Modules.Rosters.Domain.Enums;

/// <summary>
/// Статус заявки игрока. Заявка не редактируется задним числом: при уходе закрывается с нужным
/// статусом и датой, история сохраняется (documentation/doc.md §7). Удалить можно только
/// ошибочную заявку, по которой ещё нет событий в матчах.
/// </summary>
public enum PlayerRegistrationStatus
{
    Active = 0, // Играет за команду
    TransferredOut = 1, // Перешёл в другую команду этого турнира
    Released = 2 // Отзаявлен (или команда снята с турнира)
}
