using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stathub.Infrastructure.Storage;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Modules.Leagues.Application;
using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.Shared.Exceptions;
using Stathub.WebApi.Contracts.Leagues;
using Stathub.WebApi.Infrastructure.Auth;
using Stathub.WebApi.Infrastructure.Uploads;

namespace Stathub.WebApi.Controllers.Leagues;

/// <summary>
/// Лиги организации. Организатор берётся из токена: тело запроса и query его больше не задают,
/// иначе любой мог бы создать лигу от имени чужой организации.
/// </summary>
[ApiController]
[Route("leagues")]
[Authorize(Policy = AuthPolicies.Organizer)]
public sealed class LeaguesController(
    LeagueService leagueService,
    ILogoStorage logoStorage,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateLeague(CreateLeagueRequest request, CancellationToken cancellationToken)
    {
        var leagueId = await leagueService.CreateLeagueAsync(
            currentUser.TenantId,
            request.Name,
            request.Slug,
            request.Sport,
            request.City,
            request.Region,
            request.DataSource,
            cancellationToken);

        return CreatedAtAction(nameof(GetLeague), new { id = leagueId }, new { id = leagueId });
    }

    /// <summary>Список лиг организации из токена; query-параметр organizerId игнорируется.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeagueDto>>> ListLeagues(CancellationToken cancellationToken) =>
        Ok(await leagueService.ListAsync(currentUser.TenantId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LeagueDto>> GetLeague(Guid id, CancellationToken cancellationToken) =>
        Ok(await GetOwnedLeagueAsync(id, cancellationToken));

    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<LeagueDto>> GetLeagueBySlug(string slug, CancellationToken cancellationToken)
    {
        var league = await leagueService.GetBySlugAsync(slug, cancellationToken);

        if (league.OrganizerId != currentUser.TenantId)
            throw new NotFoundException($"Лига '{slug}' не найдена.");

        return Ok(league);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LeagueDto>> UpdateLeague(
        Guid id,
        UpdateLeagueRequest request,
        CancellationToken cancellationToken)
    {
        await GetOwnedLeagueAsync(id, cancellationToken);

        return Ok(await leagueService.UpdateAsync(
            id, request.Name, request.Slug, request.City, request.Region, cancellationToken));
    }

    /// <summary>Удаление пустой лиги. 409 — в лиге есть турниры (их можно удалить или архивировать лигу).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteLeague(Guid id, CancellationToken cancellationToken)
    {
        await GetOwnedLeagueAsync(id, cancellationToken);

        var logoUrl = await leagueService.DeleteAsync(id, cancellationToken);
        await logoStorage.DeleteAsync(logoUrl, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> PublishLeague(Guid id, CancellationToken cancellationToken)
    {
        await GetOwnedLeagueAsync(id, cancellationToken);
        await leagueService.PublishAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> ArchiveLeague(Guid id, CancellationToken cancellationToken)
    {
        await GetOwnedLeagueAsync(id, cancellationToken);
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

        var current = await GetOwnedLeagueAsync(id, cancellationToken);

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
        var current = await GetOwnedLeagueAsync(id, cancellationToken);
        var league = await leagueService.SetLogoAsync(id, null, cancellationToken);

        await logoStorage.DeleteAsync(current.LogoUrl, cancellationToken);

        return Ok(league);
    }

    /// <summary>Общая проверка владения: и отсутствующая, и чужая лига дают 404.</summary>
    private async Task<LeagueDto> GetOwnedLeagueAsync(Guid id, CancellationToken cancellationToken)
    {
        var league = await leagueService.GetByIdAsync(id, cancellationToken);

        if (league.OrganizerId != currentUser.TenantId)
            throw new NotFoundException($"Лига '{id}' не найдена.");

        return league;
    }
}
