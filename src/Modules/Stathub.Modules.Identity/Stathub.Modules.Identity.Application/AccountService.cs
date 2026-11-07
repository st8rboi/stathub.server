using Stathub.Modules.Identity.Application.Dtos;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Modules.Identity.Domain.Entities;
using Stathub.Modules.Identity.Domain.Enums;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Identity.Application;

/// <summary>
/// Административное управление аккаунтами: платформа создаёт организатора,
/// организатор — судей и тренеров своей организации, а также назначает их на матчи.
/// </summary>
public sealed class AccountService(
    IUserRepository users,
    ITenantRepository tenants,
    IUserRoleAssignmentRepository assignments,
    IPasswordHasher passwordHasher)
{
    /// <summary>Ops-сценарий: новая организация и её владелец. Публичной регистрации нет.</summary>
    public async Task<(Tenant Tenant, User User)> CreateTenantWithOwnerAsync(
        string tenantName,
        string? tenantSlug,
        string login,
        string displayName,
        string password,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        var slug = Tenant.NormalizeSlug(tenantSlug, tenantName);
        if (await tenants.GetBySlugAsync(slug, cancellationToken) is not null)
            throw new ConflictException($"Организация со slug '{slug}' уже существует.");

        var normalizedLogin = User.NormalizeLogin(login);
        if (await users.GetByLoginAsync(normalizedLogin, cancellationToken) is not null)
            throw new ConflictException($"Логин '{normalizedLogin}' уже занят.");

        AuthService.EnsurePasswordStrength(password);

        var tenant = Tenant.Create(tenantName, slug);
        var user = User.Create(login, displayName, passwordHasher.Hash(password), email);
        var assignment = UserRoleAssignment.Create(user.Id, tenant.Id, UserRole.Organizer, RoleScopeType.Tenant);

        await tenants.AddAsync(tenant, cancellationToken);
        await users.AddAsync(user, cancellationToken);
        await assignments.AddAsync(assignment, cancellationToken);
        await tenants.SaveChangesAsync(cancellationToken);

        return (tenant, user);
    }

    /// <summary>Организатор создаёт аккаунт сотрудника (судья/тренер/админ) в своей организации.</summary>
    public async Task<UserSummaryDto> CreateUserAsync(
        Guid tenantId,
        Guid grantedByUserId,
        string login,
        string displayName,
        string password,
        UserRole role,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        if (role == UserRole.Organizer)
            throw new DomainException("Второго организатора выдаёт только платформа.");

        var normalizedLogin = User.NormalizeLogin(login);
        if (await users.GetByLoginAsync(normalizedLogin, cancellationToken) is not null)
            throw new ConflictException($"Логин '{normalizedLogin}' уже занят.");

        AuthService.EnsurePasswordStrength(password);

        var user = User.Create(login, displayName, passwordHasher.Hash(password), email);
        var assignment = UserRoleAssignment.Create(user.Id, tenantId, role, RoleScopeType.Tenant, grantedByUserId: grantedByUserId);

        await users.AddAsync(user, cancellationToken);
        await assignments.AddAsync(assignment, cancellationToken);
        await users.SaveChangesAsync(cancellationToken);

        return new UserSummaryDto(user.Id, user.Login, user.DisplayName, user.Email, user.IsActive, role, user.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<UserSummaryDto>> ListUsersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantAssignments = await assignments.GetActiveByTenantAsync(tenantId, cancellationToken);
        if (tenantAssignments.Count == 0)
            return [];

        var userIds = tenantAssignments.Select(a => a.UserId).Distinct().ToList();
        var userList = await users.GetByIdsAsync(userIds, cancellationToken);

        var roleByUser = tenantAssignments
            .GroupBy(a => a.UserId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(a => a.ScopeType == RoleScopeType.Tenant ? 0 : 1)
                    .ThenBy(a => (int)a.Role)
                    .Select(a => a.Role)
                    .First());

        return userList
            .Select(user => new UserSummaryDto(
                user.Id,
                user.Login,
                user.DisplayName,
                user.Email,
                user.IsActive,
                roleByUser[user.Id],
                user.CreatedAtUtc))
            .OrderBy(user => user.DisplayName)
            .ToList();
    }

    /// <summary>Назначение на конкретный объект: матч (судья), команду (тренер), лигу/турнир (админ).</summary>
    public async Task<AssignmentSummaryDto> AssignScopeAsync(
        Guid tenantId,
        Guid userId,
        UserRole role,
        RoleScopeType scopeType,
        Guid? scopeId,
        Guid grantedByUserId,
        CancellationToken cancellationToken = default)
    {
        var userAssignments = await assignments.GetActiveByUserAsync(userId, cancellationToken);
        if (!userAssignments.Any(a => a.TenantId == tenantId))
            throw new DomainException("Пользователь не состоит в этой организации.");

        var assignment = UserRoleAssignment.Create(
            userId,
            tenantId,
            role,
            scopeType,
            scopeId,
            grantedByUserId);

        await assignments.AddAsync(assignment, cancellationToken);
        await assignments.SaveChangesAsync(cancellationToken);

        var user = await users.GetByIdAsync(userId, cancellationToken)!;

        return new AssignmentSummaryDto(
            assignment.Id,
            userId,
            user?.Login ?? string.Empty,
            user?.DisplayName ?? string.Empty,
            role,
            scopeType,
            scopeId,
            assignment.GrantedAtUtc);
    }

    /// <summary>Матчи, на которые назначен пользователь (для «Мои матчи» судьи).</summary>
    public async Task<IReadOnlyList<Guid>> GetAssignedMatchIdsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var userAssignments = await assignments.GetActiveByUserAsync(userId, cancellationToken);

        return userAssignments
            .Where(assignment => assignment.TenantId == tenantId
                                 && assignment.ScopeType == RoleScopeType.Match
                                 && assignment.ScopeId is not null)
            .Select(assignment => assignment.ScopeId!.Value)
            .Distinct()
            .ToList();
    }

    public async Task RevokeAsync(Guid tenantId, Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var assignment = await assignments.GetByIdAsync(assignmentId, cancellationToken)
            ?? throw new NotFoundException("Назначение не найдено.");

        if (assignment.TenantId != tenantId)
            throw new NotFoundException("Назначение не найдено.");

        assignment.Revoke();
        await assignments.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssignmentSummaryDto>> ListAssignmentsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantAssignments = await assignments.GetActiveByTenantAsync(tenantId, cancellationToken);
        if (tenantAssignments.Count == 0)
            return [];

        var userIds = tenantAssignments.Select(a => a.UserId).Distinct().ToList();
        var userList = await users.GetByIdsAsync(userIds, cancellationToken);
        var usersById = userList.ToDictionary(user => user.Id);

        return tenantAssignments
            .Select(assignment => new AssignmentSummaryDto(
                assignment.Id,
                assignment.UserId,
                usersById.TryGetValue(assignment.UserId, out var user) ? user.Login : string.Empty,
                user?.DisplayName ?? string.Empty,
                assignment.Role,
                assignment.ScopeType,
                assignment.ScopeId,
                assignment.GrantedAtUtc))
            .OrderBy(assignment => assignment.ScopeType)
            .ThenBy(assignment => assignment.UserDisplayName)
            .ToList();
    }
}
