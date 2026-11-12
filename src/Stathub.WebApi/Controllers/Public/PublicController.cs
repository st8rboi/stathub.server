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

    /// <summary>Дни с матчами (в часовом поясе <paramref name="timeZone"/>, IANA) — полоса дат витрины.</summary>
    [HttpGet("match-days")]
    public async Task<ActionResult<IReadOnlyList<PublicMatchDayDto>>> GetMatchDays(
        [FromQuery] string? timeZone,
        CancellationToken cancellationToken) =>
        Ok(await publicReadService.ListMatchDaysAsync(timeZone, cancellationToken));

    [HttpGet("matches/{id:guid}")]
    public async Task<ActionResult<PublicMatchPageDto>> GetMatch(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await publicReadService.GetMatchPageAsync(id, cancellationToken));

    [HttpGet("live")]
    public async Task<ActionResult<IReadOnlyList<PublicMatchDto>>> GetLive(CancellationToken cancellationToken) =>
        Ok(await publicReadService.ListLiveAsync(cancellationToken));

    /// <summary>
    /// Страница команды: участия в опубликованных этапах; матчи, таблица и состав —
    /// по <paramref name="stageId"/> или, если он не указан, по самому свежему этапу.
    /// </summary>
    [HttpGet("teams/{id:guid}")]
    public async Task<ActionResult<PublicTeamPageDto>> GetTeam(
        Guid id,
        [FromQuery] Guid? stageId,
        CancellationToken cancellationToken) =>
        Ok(await publicReadService.GetTeamPageAsync(id, stageId, cancellationToken));

    /// <summary>
    /// Статистика игроков: голы, автоголы, карточки и матчи с событиями. Считается по дивизиону
    /// <paramref name="divisionId"/>; если он не указан — по первому дивизиону турнира.
    /// </summary>
    [HttpGet("tournaments/{id:guid}/statistics")]
    public async Task<ActionResult<PublicTournamentStatisticsDto>> GetTournamentStatistics(
        Guid id,
        [FromQuery] Guid? divisionId,
        CancellationToken cancellationToken) =>
        Ok(await publicReadService.GetTournamentStatisticsAsync(id, divisionId, cancellationToken));

    /// <summary>
    /// Полная статистика игроков дивизиона файлом .xlsx (только текст, без фото и логотипов):
    /// на витрине показывается топ-10, остальное — здесь.
    /// </summary>
    [HttpGet("tournaments/{id:guid}/statistics.xlsx")]
    public async Task<IActionResult> DownloadTournamentStatistics(
        Guid id,
        [FromQuery] Guid? divisionId,
        CancellationToken cancellationToken)
    {
        var statistics = await publicReadService.GetTournamentStatisticsAsync(id, divisionId, cancellationToken);
        var (fileName, content) = StatisticsWorkbook.Build(statistics, DateTime.UtcNow);

        return File(content, StatisticsWorkbook.ContentType, fileName);
    }
}
