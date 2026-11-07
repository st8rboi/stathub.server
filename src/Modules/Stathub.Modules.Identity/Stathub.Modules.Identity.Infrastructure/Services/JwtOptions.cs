namespace Stathub.Modules.Identity.Infrastructure.Services;

/// <summary>Параметры JWT из конфигурации (<c>Jwt</c>). Ключ обязателен и не короче 32 символов.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "stathub";

    public string Audience { get; init; } = "stathub-api";

    public string Key { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 15;

    public int RefreshTokenDays { get; init; } = 30;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key) || Key.Length < 32)
            throw new InvalidOperationException(
                "Jwt:Key не настроен или короче 32 символов. Задайте его в appsettings.Development.json " +
                "или переменной окружения Jwt__Key.");

        if (AccessTokenMinutes is <= 0 or > 1440)
            throw new InvalidOperationException("Jwt:AccessTokenMinutes должен быть от 1 до 1440.");

        if (RefreshTokenDays is <= 0 or > 365)
            throw new InvalidOperationException("Jwt:RefreshTokenDays должен быть от 1 до 365.");
    }
}
