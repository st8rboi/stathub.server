using Stathub.Modules.Identity.Domain.Enums;

namespace Stathub.WebApi.Contracts.Auth;

public sealed record LoginRequest(string Login, string Password, Guid? TenantId);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Создание аккаунта организатором: судья, тренер или админ организации.</summary>
public sealed record CreateAccountRequest(
    string Login,
    string DisplayName,
    string Password,
    UserRole Role,
    string? Email);

/// <summary>Назначение роли на конкретный объект: матч, команду, лигу или турнир.</summary>
public sealed record AssignScopeRequest(
    Guid UserId,
    UserRole Role,
    RoleScopeType ScopeType,
    Guid? ScopeId);
