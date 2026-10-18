using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Modules.Identity.Domain.Entities;
using Stathub.Modules.Identity.Domain.Enums;

namespace Stathub.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Выпуск пары токенов. В access-токене только «тонкие» claims: кто, в какой организации и с какой
/// базовой ролью. Права на конкретные матчи/команды проверяются по БД — иначе отзыв назначения
/// не действовал бы до истечения токена.
/// </summary>
internal sealed class JwtTokenIssuer(JwtOptions options) : ITokenIssuer
{
    public IssuedTokens Issue(User user, Tenant tenant, UserRole role)
    {
        var now = DateTime.UtcNow;
        var accessExpiresAtUtc = now.AddMinutes(options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Login),
            new("tenant_id", tenant.Id.ToString()),
            new("role", role.ToString()),
            new("name", user.DisplayName)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now,
            expires: accessExpiresAtUtc,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        // Refresh-токен — непрозрачная случайная строка; в БД попадёт только её хеш.
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

        return new IssuedTokens(
            accessToken,
            accessExpiresAtUtc,
            refreshToken,
            now.AddDays(options.RefreshTokenDays));
    }
}
