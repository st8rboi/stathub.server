using Stathub.Modules.Identity.Domain.Entities;
using Stathub.Modules.Identity.Domain.Enums;

namespace Stathub.Modules.Identity.Application.Interfaces;

/// <summary>Хеширование паролей. Реализация — BCrypt в Infrastructure.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}

/// <summary>Пара токенов одной сессии.</summary>
public sealed record IssuedTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

/// <summary>Выпуск JWT. Реализация — Infrastructure (ключ и параметры приходят из конфигурации).</summary>
public interface ITokenIssuer
{
    IssuedTokens Issue(User user, Tenant tenant, UserRole role);
}
