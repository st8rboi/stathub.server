namespace Stathub.Modules.Identity.Domain.Enums;

public enum TenantStatus
{
    /// <summary>Организация работает: может создавать лиги и назначать людей.</summary>
    Active = 0,

    /// <summary>Организация заблокирована платформой: вход в админку запрещён, публичные данные остаются.</summary>
    Suspended = 1
}
