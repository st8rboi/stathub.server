using System.Security.Cryptography;
using System.Text;
using Stathub.Modules.Identity.Application.Dtos;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Modules.Identity.Domain.Entities;
using Stathub.Modules.Identity.Domain.Enums;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Identity.Application;

/// <summary>
/// Вход и сессии. Публичной регистрации нет: только существующие аккаунты, выданные
/// административно. Пароль проверяется BCrypt-хешем, refresh-токены хранятся хешем и ротируются.
/// </summary>
public sealed class AuthService(
    IUserRepository users,
    ITenantRepository tenants,
    IUserRoleAssignmentRepository assignments,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer)
{
    public const int PasswordMinLength = 8;

    private static readonly TimeSpan RefreshReuseWindow = TimeSpan.FromMinutes(2);

    public async Task<AuthResultDto> LoginAsync(
        string login,
        string password,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var user = await users.GetByLoginAsync(User.NormalizeLogin(login), cancellationToken)
            ?? throw new DomainException("Неверный логин или пароль.");

        if (string.IsNullOrWhiteSpace(password) || !passwordHasher.Verify(password, user.PasswordHash))
            throw new DomainException("Неверный логин или пароль.");

        // После проверки пароля: иначе по одному логину можно узнать, что аккаунт существует и отключён.
        if (!user.IsActive)
            throw new DomainException("Аккаунт отключён.");

        var memberships = await LoadMembershipsAsync(user.Id, cancellationToken);
        if (memberships.Count == 0)
            throw new DomainException("У аккаунта нет доступа ни к одной организации.");

        var membership = tenantId is { } requestedId
            ? memberships.FirstOrDefault(m => m.TenantId == requestedId)
              ?? throw new DomainException("Нет доступа к выбранной организации.")
            : memberships
                .OrderBy(m => m.Role == UserRole.Organizer ? 0 : 1)
                .First();

        if (membership.Tenant.Status != TenantStatus.Active)
            throw new DomainException("Организация заблокирована.");

        user.MarkLogin(DateTime.UtcNow);
        await users.SaveChangesAsync(cancellationToken);

        var issued = tokenIssuer.Issue(user, membership.Tenant, membership.Role);
        await StoreRefreshTokenAsync(user.Id, membership.Tenant.Id, issued, cancellationToken);

        return ToResult(issued, user, membership, memberships);
    }

    /// <summary>Обновление сессии с ротацией: старый refresh-токен отзывается, выдаётся новый.</summary>
    public async Task<AuthResultDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new DomainException("Refresh-токен обязателен.");

        var stored = await refreshTokens.GetByHashAsync(HashToken(refreshToken), cancellationToken)
            ?? throw new DomainException("Сессия не найдена, войдите заново.");

        var nowUtc = DateTime.UtcNow;

        if (!stored.IsActive(nowUtc) && !stored.CanBeUsedAfterRotation(nowUtc, RefreshReuseWindow))
            throw new DomainException("Сессия истекла, войдите заново.");

        var user = await users.GetByIdAsync(stored.UserId, cancellationToken)
            ?? throw new DomainException("Сессия не найдена, войдите заново.");

        if (!user.IsActive)
            throw new DomainException("Аккаунт отключён.");

        var memberships = await LoadMembershipsAsync(user.Id, cancellationToken);
        var membership = memberships.FirstOrDefault(m => m.TenantId == stored.TenantId)
            ?? throw new DomainException("Доступ к организации отозван.");

        if (membership.Tenant.Status != TenantStatus.Active)
            throw new DomainException("Организация заблокирована.");

        stored.Rotate(nowUtc);
        var issued = tokenIssuer.Issue(user, membership.Tenant, membership.Role);

        await StoreRefreshTokenAsync(user.Id, membership.Tenant.Id, issued, cancellationToken);
        await refreshTokens.SaveChangesAsync(cancellationToken);

        return ToResult(issued, user, membership, memberships);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var stored = await refreshTokens.GetByHashAsync(HashToken(refreshToken), cancellationToken);
        // Заменённый токен тоже: выход закрывает ему окно повторного использования.
        if (stored is null)
            return;

        stored.Revoke();
        await refreshTokens.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Пользователь не найден.");

        if (!passwordHasher.Verify(currentPassword, user.PasswordHash))
            throw new DomainException("Текущий пароль неверен.");

        EnsurePasswordStrength(newPassword);

        user.SetPasswordHash(passwordHasher.Hash(newPassword));

        // Смена пароля выкидывает все устройства: украденный refresh-токен перестаёт работать.
        await refreshTokens.RevokeAllForUserAsync(userId, cancellationToken);
        await users.SaveChangesAsync(cancellationToken);
        await refreshTokens.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Профиль для текущей сессии. <paramref name="tenantId"/> — организация из токена: профиль
    /// должен описывать ту же организацию, в которой работает сессия, а не «главную» у аккаунта.
    /// </summary>
    public async Task<UserProfileDto> GetProfileAsync(
        Guid userId,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Пользователь не найден.");

        var memberships = await LoadMembershipsAsync(userId, cancellationToken);
        var primary = memberships.FirstOrDefault(m => m.TenantId == tenantId)
            ?? memberships.OrderBy(m => m.Role == UserRole.Organizer ? 0 : 1).FirstOrDefault()
            ?? throw new DomainException("У аккаунта нет доступа ни к одной организации.");

        return ToProfile(user, primary, memberships);
    }

    public static void EnsurePasswordStrength(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < PasswordMinLength)
            throw new DomainException($"Пароль должен быть не короче {PasswordMinLength} символов.");
    }

    /// <summary>SHA-256 от токена: в БД лежит только хеш, поиск идёт по нему.</summary>
    public static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task StoreRefreshTokenAsync(
        Guid userId,
        Guid tenantId,
        IssuedTokens issued,
        CancellationToken cancellationToken)
    {
        var refreshToken = RefreshToken.Create(
            userId,
            tenantId,
            HashToken(issued.RefreshToken),
            issued.RefreshTokenExpiresAtUtc);

        await refreshTokens.AddAsync(refreshToken, cancellationToken);
        await refreshTokens.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Membership>> LoadMembershipsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var userAssignments = await assignments.GetActiveByUserAsync(userId, cancellationToken);
        if (userAssignments.Count == 0)
            return [];

        var tenantIds = userAssignments.Select(a => a.TenantId).Distinct().ToList();
        var tenantList = await tenants.GetByIdsAsync(tenantIds, cancellationToken);
        var tenantsById = tenantList.ToDictionary(tenant => tenant.Id);

        return userAssignments
            .Where(a => tenantsById.ContainsKey(a.TenantId))
            .GroupBy(a => a.TenantId)
            .Select(group => new Membership(
                tenantsById[group.Key],
                ResolveRole(group)))
            .ToList();
    }

    /// <summary>
    /// Базовая роль в организации: роль со скоупом Tenant, если есть (организатор/админ),
    /// иначе — самая «старшая» из назначений (судья/тренер).
    /// </summary>
    private static UserRole ResolveRole(IEnumerable<UserRoleAssignment> assignments) =>
        assignments
            .OrderBy(a => a.ScopeType == RoleScopeType.Tenant ? 0 : 1)
            .ThenBy(a => (int)a.Role)
            .Select(a => a.Role)
            .First();

    private static UserProfileDto ToProfile(User user, Membership membership, IReadOnlyList<Membership> all) => new(
        user.Id,
        user.Login,
        user.DisplayName,
        user.Email,
        membership.Tenant.Id,
        membership.Tenant.Name,
        membership.Tenant.Slug,
        membership.Role,
        all.Select(m => new MembershipDto(m.Tenant.Id, m.Tenant.Name, m.Tenant.Slug, m.Role)).ToList());

    private static AuthResultDto ToResult(
        IssuedTokens issued,
        User user,
        Membership membership,
        IReadOnlyList<Membership> all) => new(
        issued.AccessToken,
        issued.AccessTokenExpiresAtUtc,
        issued.RefreshToken,
        issued.RefreshTokenExpiresAtUtc,
        ToProfile(user, membership, all));

    private sealed record Membership(Tenant Tenant, UserRole Role)
    {
        public Guid TenantId => Tenant.Id;
    }
}
