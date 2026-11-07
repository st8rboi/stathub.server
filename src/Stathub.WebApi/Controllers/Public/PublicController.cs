using Microsoft.AspNetCore.Mvc;
using Stathub.WebApi.Contracts.Public;
using Stathub.WebApi.Infrastructure.Public;

namespace Stathub.WebApi.Controllers.Public;

/// <summary>
/// Публичная витрина (аналог Flashscore): read-only, без аутентификации, только опубликованные лиги.
/// </summary>
[ApiController]
[Route("public")]
public sealed class PublicController(PublicReadService publicReadService) : ControllerBase
{
    [HttpGet("leagues")]
    public async Task<ActionResult<IReadOnlyList<PublicLeagueDto>>> GetLeagues(
        [FromQuery] string? query,
        [FromQuery] string? city,
        CancellationToken cancellationToken) =>
        Ok(await publicReadService.ListLeaguesAsync(query, city, cancellationToken));

    [HttpGet("leagues/{slug}")]
    public async Task<ActionResult<PublicLeaguePageDto>> GetLeague(
        string slug,
        CancellationToken cancellationToken) =>
        Ok(await publicReadService.GetLeaguePageAsync(slug, cancellationToken));

    [HttpGet("stages/{id:guid}")]
    public async Task<ActionResult<PublicStagePageDto>> GetStage(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await publicReadService.GetStagePageAsync(id, cancellationToken));

    [HttpGet("matches")]
    public async Task<ActionResult<IReadOnlyList<PublicMatchDto>>> GetMatches(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken) =>
        Ok(await publicReadService.ListMatchesAsync(fromUtc, toUtc, cancellationToken));

    [HttpGet("matches/{id:guid}")]
    public async Task<ActionResult<PublicMatchPageDto>> GetMatch(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await publicReadService.GetMatchPageAsync(id, cancellationToken));

    [HttpGet("live")]
    public async Task<ActionResult<IReadOnlyList<PublicMatchDto>>> GetLive(CancellationToken cancellationToken) =>
        Ok(await publicReadService.ListLiveAsync(cancellationToken));

    /// <summary>Статистика игроков турнира: голы, автоголы, карточки и матчи с событиями.</summary>
    [HttpGet("tournaments/{id:guid}/statistics")]
    public async Task<ActionResult<PublicTournamentStatisticsDto>> GetTournamentStatistics(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await publicReadService.GetTournamentStatisticsAsync(id, cancellationToken));
}
