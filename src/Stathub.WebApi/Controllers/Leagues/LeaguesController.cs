using Microsoft.AspNetCore.Mvc;
using Stathub.Infrastructure.Storage;
using Stathub.Modules.Leagues.Application;
using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.WebApi.Contracts.Leagues;
using Stathub.WebApi.Infrastructure.Uploads;

namespace Stathub.WebApi.Controllers.Leagues;

[ApiController]
[Route("leagues")]
public sealed class LeaguesController(LeagueService leagueService, ILogoStorage logoStorage) : ControllerBase
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

    /// <summary>Загрузка логотипа лиги: файл — в хранилище, в БД — только путь.</summary>
    [HttpPost("{id:guid}/logo")]
    // Лимит выше бизнес-лимита, чтобы размер проверяла наша валидация (см. LogoFile).
    [RequestSizeLimit(LogoFile.MaxRequestBytes)]
    [RejectTooLargeLogo]
    public async Task<ActionResult<LeagueDto>> UploadLogo(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var error = LogoFile.Validate(file);
        if (error is not null)
            return Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);

        var current = await leagueService.GetByIdAsync(id, cancellationToken);

        await using var stream = file.OpenReadStream();

        string logoUrl;
        try
        {
            logoUrl = await logoStorage.SaveAsync(stream, LogoFile.ExtensionFor(file.ContentType), cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Problem(
                detail: "Не удалось сохранить файл логотипа на сервере. Проверьте права на каталог wwwroot/uploads.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var league = await leagueService.SetLogoAsync(id, logoUrl, cancellationToken);

        await logoStorage.DeleteAsync(current.LogoUrl, cancellationToken);

        return Ok(league);
    }

    [HttpDelete("{id:guid}/logo")]
    public async Task<ActionResult<LeagueDto>> DeleteLogo(Guid id, CancellationToken cancellationToken)
    {
        var current = await leagueService.GetByIdAsync(id, cancellationToken);
        var league = await leagueService.SetLogoAsync(id, null, cancellationToken);

        await logoStorage.DeleteAsync(current.LogoUrl, cancellationToken);

        return Ok(league);
    }
}
