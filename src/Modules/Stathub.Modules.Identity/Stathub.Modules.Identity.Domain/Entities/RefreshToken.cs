using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Identity.Domain.Entities;

/// <summary>
/// Refresh-токен устройства. Хранится только хеш: утечка БД не даёт войти.
/// Ротация — при каждом обновлении старый токен отзывается и выпускается новый.
/// </summary>
public sealed class RefreshToken : AggregateRoot
{
    public Guid UserId { get; private set; }

    /// <summary>Тенант, в котором выдан токен: сессия всегда работает в контексте одной организации.</summary>
    public Guid TenantId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    /// <summary>Токен заменён новым при обновлении сессии; отзыв по другой причине это поле снимает.</summary>
    public DateTime? RotatedAtUtc { get; private set; }

    public string? Device { get; private set; }

    private RefreshToken()
    {
    }

    private RefreshToken(Guid id, Guid userId, Guid tenantId, string tokenHash, DateTime expiresAtUtc, string? device)
    {
        Id = id;
        UserId = userId;
        TenantId = tenantId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        Device = device;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static RefreshToken Create(
        Guid userId,
        Guid tenantId,
        string tokenHash,
        DateTime expiresAtUtc,
        string? device = null)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty)
            throw new DomainException("Пользователь и организация обязательны.");
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("Хеш refresh-токена обязателен.");
        if (expiresAtUtc <= DateTime.UtcNow)
            throw new DomainException("Срок жизни refresh-токена должен быть в будущем.");

        return new RefreshToken(Guid.NewGuid(), userId, tenantId, tokenHash, expiresAtUtc, device);
    }

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;

    public void Revoke()
    {
        RevokedAtUtc ??= DateTime.UtcNow;
        RotatedAtUtc = null;
    }

    public void Rotate(DateTime nowUtc)
    {
        RevokedAtUtc ??= nowUtc;
        RotatedAtUtc ??= nowUtc;
    }

    /// <summary>
    /// На плохой связи ответ с новым токеном может потеряться, и устройство повторит запрос
    /// со старым. Короткое окно после ротации не даёт из-за этого разлогинить судью посреди матча.
    /// </summary>
    public bool CanBeUsedAfterRotation(DateTime nowUtc, TimeSpan window) =>
        RotatedAtUtc is { } rotatedAtUtc && ExpiresAtUtc > nowUtc && nowUtc - rotatedAtUtc <= window;
}
