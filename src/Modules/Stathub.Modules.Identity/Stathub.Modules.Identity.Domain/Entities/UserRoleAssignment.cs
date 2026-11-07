using Stathub.Modules.Identity.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Identity.Domain.Entities;

/// <summary>
/// Назначение роли внутри организации. Роль без скоупа (Tenant) означает «сотрудник организации»,
/// роль с скоупом Match — «судья этого матча». Отзыв не удаляет запись: история назначений
/// остаётся, просто перестаёт действовать.
/// </summary>
public sealed class UserRoleAssignment : AggregateRoot
{
    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public UserRole Role { get; private set; }

    public RoleScopeType ScopeType { get; private set; }

    /// <summary>Id лиги/турнира/команды/матча; null только для <see cref="RoleScopeType.Tenant"/>.</summary>
    public Guid? ScopeId { get; private set; }

    public Guid? GrantedByUserId { get; private set; }

    public DateTime GrantedAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public bool IsActive => RevokedAtUtc is null;

    private UserRoleAssignment()
    {
    }

    private UserRoleAssignment(
        Guid id,
        Guid userId,
        Guid tenantId,
        UserRole role,
        RoleScopeType scopeType,
        Guid? scopeId,
        Guid? grantedByUserId)
    {
        Id = id;
        UserId = userId;
        TenantId = tenantId;
        Role = role;
        ScopeType = scopeType;
        ScopeId = scopeId;
        GrantedByUserId = grantedByUserId;
        GrantedAtUtc = DateTime.UtcNow;
    }

    public static UserRoleAssignment Create(
        Guid userId,
        Guid tenantId,
        UserRole role,
        RoleScopeType scopeType,
        Guid? scopeId = null,
        Guid? grantedByUserId = null)
    {
        if (userId == Guid.Empty)
            throw new DomainException("Пользователь обязателен.");
        if (tenantId == Guid.Empty)
            throw new DomainException("Организация обязательна.");
        if (!Enum.IsDefined(role))
            throw new DomainException($"Неизвестная роль '{role}'.");
        if (!Enum.IsDefined(scopeType))
            throw new DomainException($"Неизвестный скоуп роли '{scopeType}'.");

        // Скоуп Tenant идёт без ScopeId, все остальные — строго с ним: «роль вообще» и
        // «роль на конкретный матч» не должны смешиваться.
        if (scopeType == RoleScopeType.Tenant && scopeId is not null)
            throw new DomainException("Роль организации не может иметь ScopeId.");

        if (scopeType != RoleScopeType.Tenant && (scopeId is null || scopeId == Guid.Empty))
            throw new DomainException($"Для скоупа '{scopeType}' обязателен ScopeId.");

        return new UserRoleAssignment(
            Guid.NewGuid(),
            userId,
            tenantId,
            role,
            scopeType,
            scopeId,
            grantedByUserId);
    }

    public void Revoke()
    {
        if (!IsActive)
            throw new ConflictException("Назначение уже отозвано.");

        RevokedAtUtc = DateTime.UtcNow;
    }
}
