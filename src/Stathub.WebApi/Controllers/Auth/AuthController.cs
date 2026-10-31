using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Stathub.Modules.Identity.Application;
using Stathub.Modules.Identity.Application.Dtos;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Shared.Exceptions;
using Stathub.WebApi.Contracts.Auth;
using Stathub.WebApi.Infrastructure.Auth;

namespace Stathub.WebApi.Controllers.Auth;

/// <summary>
/// Вход и сессии. Публичной регистрации нет: аккаунты выдаются административно
/// (организатор — платформой, судья — организатором).
/// </summary>
[ApiController]
[Route("auth")]
public sealed class AuthController(
    AuthService authService,
    LoginThrottle loginThrottle,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LoginThrottle.IpPolicy)]
    public async Task<ActionResult<AuthResultDto>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress;

        if (!loginThrottle.TryBeginAttempt(request.Login, ip, out var retryAfter))
        {
            var seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            Response.Headers.RetryAfter = seconds.ToString();

            return Problem(
                detail: $"Слишком много неудачных попыток входа. Повторите через {Math.Max(1, seconds / 60)} мин.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        try
        {
            var result = await authService.LoginAsync(request.Login, request.Password, request.TenantId, cancellationToken);
            loginThrottle.Reset(request.Login, ip);

            return Ok(result);
        }
        catch (Exception exception) when (exception is not DomainException)
        {
            loginThrottle.ReleaseAttempt(request.Login, ip);
            throw;
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken) =>
        Ok(await authService.RefreshAsync(request.RefreshToken, cancellationToken));

    /// <summary>Идемпотентно: повторный выход с тем же токеном не считается ошибкой.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserProfileDto>> Me(CancellationToken cancellationToken) =>
        Ok(await authService.GetProfileAsync(currentUser.UserId, cancellationToken));

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await authService.ChangePasswordAsync(
            currentUser.UserId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        return NoContent();
    }
}
