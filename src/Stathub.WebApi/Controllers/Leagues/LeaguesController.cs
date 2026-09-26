using Microsoft.AspNetCore.Mvc;
using Stathub.Modules.Leagues.Application;
using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.WebApi.Contracts.Leagues;

namespace Stathub.WebApi.Controllers.Leagues;

[ApiController]
[Route("leagues")]
public sealed class LeaguesController(LeagueService leagueService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateLeague(CreateLeagueRequest request, CancellationToken cancellationToken)
    {
        var leagueId = await leagueService.CreateLeagueAsync(
            request.OrganizerId,
            request.Name,
            request.Slug,
            request.Sport,
            request.City,
            request.Region,
            request.DataSource,
            cancellationToken);

        return CreatedAtAction(nameof(GetLeague), new { id = leagueId }, new { id = leagueId });
    }

    // Без organizerId — публичный список опубликованных лиг, с organizerId — все лиги организатора
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeagueDto>>> ListLeagues(
        [FromQuery] Guid? organizerId,
        CancellationToken cancellationToken) =>
        Ok(await leagueService.ListAsync(organizerId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LeagueDto>> GetLeague(Guid id, CancellationToken cancellationToken) =>
        Ok(await leagueService.GetByIdAsync(id, cancellationToken));

    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<LeagueDto>> GetLeagueBySlug(string slug, CancellationToken cancellationToken) =>
        Ok(await leagueService.GetBySlugAsync(slug, cancellationToken));

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> PublishLeague(Guid id, CancellationToken cancellationToken)
    {
        await leagueService.PublishAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> ArchiveLeague(Guid id, CancellationToken cancellationToken)
    {
        await leagueService.ArchiveAsync(id, cancellationToken);
        return NoContent();
    }
}
