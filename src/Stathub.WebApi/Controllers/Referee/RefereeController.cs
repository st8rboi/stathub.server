using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stathub.WebApi.Contracts.Referee;
using Stathub.WebApi.Infrastructure.Auth;
using Stathub.WebApi.Infrastructure.Referee;

namespace Stathub.WebApi.Controllers.Referee;

/// <summary>Экран судьи: назначенные ему матчи. Организатору список тоже доступен (пустой).</summary>
[ApiController]
[Route("referee")]
[Authorize(Policy = AuthPolicies.TenantMember)]
public sealed class RefereeController(RefereeReadService refereeReadService) : ControllerBase
{
    [HttpGet("matches")]
    public async Task<ActionResult<IReadOnlyList<RefereeMatchDto>>> MyMatches(CancellationToken cancellationToken) =>
        Ok(await refereeReadService.ListMyMatchesAsync(cancellationToken));
}
