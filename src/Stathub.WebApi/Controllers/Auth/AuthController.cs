using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stathub.Modules.Identity.Application;
using Stathub.Modules.Identity.Application.Dtos;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.WebApi.Contracts.Auth;

namespace Stathub.WebApi.Controllers.Auth;

/// <summary>
/// Вход и сессии. Публичной регистрации нет: аккаунты выдаются административно
/// (организатор — платформой, судья — организатором).
/// </summary>
[ApiController]
[Route("auth")]
public sealed class AuthController(AuthService authService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await authService.LoginAsync(request.Login, request.Password, request.TenantId, cancellationToken));

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
