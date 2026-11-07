using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Modules.Identity.Domain.Entities;
using Stathub.Modules.Identity.Infrastructure.Persistence;

namespace Stathub.Modules.Identity.Infrastructure.Persistence;

internal sealed class UserRepository(IdentityDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> GetByLoginAsync(string normalizedLogin, CancellationToken cancellationToken = default) =>
        dbContext.Users.FirstOrDefaultAsync(user => user.Login == normalizedLogin, cancellationToken);

    public async Task<IReadOnlyList<User>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await dbContext.Users.AddAsync(user, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

internal sealed class TenantRepository(IdentityDbContext dbContext) : ITenantRepository
{
    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Tenants.FirstOrDefaultAsync(tenant => tenant.Id == id, cancellationToken);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        dbContext.Tenants.FirstOrDefaultAsync(tenant => tenant.Slug == slug, cancellationToken);

    public async Task<IReadOnlyList<Tenant>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        return await dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => ids.Contains(tenant.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default) =>
        await dbContext.Tenants.AddAsync(tenant, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

internal sealed class UserRoleAssignmentRepository(IdentityDbContext dbContext) : IUserRoleAssignmentRepository
{
    public async Task<IReadOnlyList<UserRoleAssignment>> GetActiveByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.UserRoleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.UserId == userId && assignment.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserRoleAssignment>> GetActiveByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await dbContext.UserRoleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId && assignment.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

    public Task<UserRoleAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.UserRoleAssignments.FirstOrDefaultAsync(assignment => assignment.Id == id, cancellationToken);

    public async Task AddAsync(UserRoleAssignment assignment, CancellationToken cancellationToken = default) =>
        await dbContext.UserRoleAssignments.AddAsync(assignment, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

internal sealed class RefreshTokenRepository(IdentityDbContext dbContext) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        dbContext.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default) =>
        await dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Заменённые токены тоже: иначе старый токен прожил бы окно повторного использования.
        var activeTokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && (token.RevokedAtUtc == null || token.RotatedAtUtc != null))
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
            token.Revoke();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
