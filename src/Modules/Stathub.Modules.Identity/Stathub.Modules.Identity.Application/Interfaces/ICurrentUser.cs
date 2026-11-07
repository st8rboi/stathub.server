using Stathub.Modules.Identity.Domain.Enums;

namespace Stathub.Modules.Identity.Application.Interfaces;

/// <summary>
/// Текущий пользователь запроса. Application не знает про HttpContext: реализация читает claims
/// в WebApi, поэтому сервисы можно вызывать из фоновых задач и тестов.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    /// <summary>Активный тенант сессии (claim <c>tenant_id</c>).</summary>
    Guid TenantId { get; }

    /// <summary>Базовая роль в активном тенанте (claim <c>role</c>).</summary>
    UserRole Role { get; }

    string Login { get; }
}
