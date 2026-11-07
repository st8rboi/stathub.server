namespace Stathub.Modules.Identity.Domain.Enums;

/// <summary>Роль человека внутри организации (тенанта).</summary>
public enum UserRole
{
    /// <summary>Владелец организации: управляет админами, тенантом и всем содержимым.</summary>
    Organizer = 0,

    /// <summary>Сотрудник организации: те же права на контент, без управления админами.</summary>
    Admin = 1,

    /// <summary>Судья: ведёт только назначенные ему матчи.</summary>
    Referee = 2,

    /// <summary>Тренер/представитель команды: заявки своей команды.</summary>
    Coach = 3
}
