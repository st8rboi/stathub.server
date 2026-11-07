using System.Security.Claims;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Modules.Identity.Domain.Enums;

namespace Stathub.WebApi.Infrastructure.Auth;

/// <summary>
/// Реализация <see cref="ICurrentUser"/> поверх HttpContext. Claims читаются «как есть»
/// (в JwtBearer выключен MapInboundClaims), поэтому имена — sub/tenant_id/role, а не длинные URI.
/// </summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId => ParseGuid(FindClaim("sub"));

    public Guid TenantId => ParseGuid(FindClaim("tenant_id"));

    public UserRole Role =>
        Enum.TryParse<UserRole>(FindClaim("role"), ignoreCase: true, out var role) ? role : UserRole.Referee;

    public string Login => FindClaim("unique_name") ?? string.Empty;

    private string? FindClaim(string type) =>
        Principal?.FindFirst(type)?.Value;

    private static Guid ParseGuid(string? value) =>
        Guid.TryParse(value, out var id) ? id : Guid.Empty;
}
