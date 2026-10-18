using Stathub.Modules.Identity.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Identity.Domain.Entities;

/// <summary>
/// Пользователь платформы. Глобальная сущность (как <c>Player</c>): не принадлежит тенанту,
/// потому что один человек может быть судьёй в нескольких лигах. Принадлежность — через
/// <see cref="UserRoleAssignment"/>.
/// <para>
/// Публичной регистрации нет: аккаунты выдаются административно (организатор — платформой,
/// судья — организатором), поэтому email необязателен, логин — произвольная строка.
/// </para>
/// </summary>
public sealed class User : AggregateRoot
{
    private const int LoginMinLength = 3;
    private const int LoginMaxLength = 64;
    private const int DisplayNameMaxLength = 100;
    private const int EmailMaxLength = 254;

    /// <summary>Логин в нижнем регистре: сравнение без учёта регистра, чтобы «Sudya» и «sudya» были одним аккаунтом.</summary>
    public string Login { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    /// <summary>Отображаемое имя (ФИО или как обращаться).</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Хеш пароля (BCrypt). Открытый пароль нигде не хранится.</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? LastLoginAtUtc { get; private set; }

    private User()
    {
    }

    private User(Guid id, string login, string displayName, string passwordHash, string? email)
    {
        Id = id;
        Login = login;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        Email = email;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static User Create(string login, string displayName, string passwordHash, string? email = null)
    {
        var normalizedLogin = NormalizeLogin(login);

        if (normalizedLogin.Length is < LoginMinLength or > LoginMaxLength)
            throw new DomainException(
                $"Логин должен быть от {LoginMinLength} до {LoginMaxLength} символов.");

        var normalizedName = (displayName ?? string.Empty).Trim();
        if (normalizedName.Length == 0)
            throw new DomainException("Имя пользователя обязательно.");
        if (normalizedName.Length > DisplayNameMaxLength)
            throw new DomainException($"Имя не может быть длиннее {DisplayNameMaxLength} символов.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Хеш пароля обязателен.");

        var normalizedEmail = NormalizeEmail(email);

        return new User(Guid.NewGuid(), normalizedLogin, normalizedName, passwordHash, normalizedEmail);
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Хеш пароля обязателен.");

        PasswordHash = passwordHash;
    }

    public void Rename(string displayName, string? email)
    {
        var normalizedName = (displayName ?? string.Empty).Trim();
        if (normalizedName.Length == 0)
            throw new DomainException("Имя пользователя обязательно.");

        if (normalizedName.Length > DisplayNameMaxLength)
            throw new DomainException($"Имя не может быть длиннее {DisplayNameMaxLength} символов.");

        DisplayName = normalizedName;
        Email = NormalizeEmail(email);
    }

    public void MarkLogin(DateTime atUtc) => LastLoginAtUtc = atUtc;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public static string NormalizeLogin(string login) => (login ?? string.Empty).Trim().ToLowerInvariant();

    private static string? NormalizeEmail(string? email)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
            return null;
        if (normalized.Length > EmailMaxLength || !normalized.Contains('@'))
            throw new DomainException("Некорректный email.");

        return normalized;
    }
}
