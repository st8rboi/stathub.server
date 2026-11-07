using Stathub.Modules.Identity.Domain.Enums;

namespace Stathub.Modules.Identity.Application.Dtos;

/// <summary>Результат входа/обновления сессии: токены + профиль для UI.</summary>
public sealed record AuthResultDto(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    UserProfileDto User);

/// <summary>Профиль активной сессии. Роль — базовая роль в активной организации.</summary>
public sealed record UserProfileDto(
    Guid Id,
    string Login,
    string DisplayName,
    string? Email,
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    UserRole Role,
    IReadOnlyList<MembershipDto> Memberships);

/// <summary>Организация, в которой у пользователя есть назначение (для переключения тенанта).</summary>
public sealed record MembershipDto(Guid TenantId, string TenantName, string TenantSlug, UserRole Role);

/// <summary>Аккаунт в списке организации.</summary>
public sealed record UserSummaryDto(
    Guid Id,
    string Login,
    string DisplayName,
    string? Email,
    bool IsActive,
    UserRole Role,
    DateTime CreatedAtUtc);

/// <summary>Выданное назначение (роль на организацию/лигу/турнир/команду/матч).</summary>
public sealed record AssignmentSummaryDto(
    Guid Id,
    Guid UserId,
    string UserLogin,
    string UserDisplayName,
    UserRole Role,
    RoleScopeType ScopeType,
    Guid? ScopeId,
    DateTime GrantedAtUtc);
