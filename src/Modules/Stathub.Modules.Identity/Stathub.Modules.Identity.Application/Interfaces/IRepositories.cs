using Stathub.Modules.Identity.Domain.Entities;

namespace Stathub.Modules.Identity.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByLoginAsync(string normalizedLogin, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tenant>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IUserRoleAssignmentRepository
{
    Task<IReadOnlyList<UserRoleAssignment>> GetActiveByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserRoleAssignment>> GetActiveByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<UserRoleAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(UserRoleAssignment assignment, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Отзывает все активные токены пользователя (смена пароля, выход со всех устройств).</summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
