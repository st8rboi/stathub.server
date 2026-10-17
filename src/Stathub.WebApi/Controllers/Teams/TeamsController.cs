using Microsoft.AspNetCore.Mvc;
using Stathub.Infrastructure.Storage;
using Stathub.Modules.Teams.Application;
using Stathub.Modules.Teams.Application.Dtos;
using Stathub.WebApi.Contracts.Teams;
using Stathub.WebApi.Infrastructure.Uploads;

namespace Stathub.WebApi.Controllers.Teams;

[ApiController]
[Route("teams")]
public sealed class TeamsController(TeamService teamService, ILogoStorage logoStorage) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateTeam(CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var teamId = await teamService.CreateAsync(request.Name, request.City, cancellationToken);

        return CreatedAtAction(
            nameof(GetTeamById),
            new { id = teamId },
            new { Id = teamId });
    }

    /// <summary>
    /// Справочник команд для выпадающих списков: по алфавиту, с необязательным поиском по названию.
    /// <paramref name="ids"/> позволяет получить конкретные команды по идентификаторам.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> GetTeams(
        [FromQuery] string? query,
        [FromQuery] string? ids,
        [FromQuery] int limit = TeamService.DefaultListLimit,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(ids))
        {
            var parsed = ids
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();

            return Ok(await teamService.GetByIdsAsync(parsed, cancellationToken));
        }

        return Ok(await teamService.ListAsync(query, limit, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TeamDto>> GetTeamById(Guid id, CancellationToken cancellationToken) =>
        Ok(await teamService.GetByIdAsync(id, cancellationToken));

    /// <summary>Загрузка логотипа команды: файл кладётся в хранилище, в БД — только путь.</summary>
    [HttpPost("{id:guid}/logo")]
    // Лимит выше бизнес-лимита (2 МБ), чтобы размер проверяла наша валидация и отвечала
    // понятным текстом, а не «Request body too large» от формата формы.
    [RequestSizeLimit(LogoFile.MaxRequestBytes)]
    [RejectTooLargeLogo]
    public async Task<ActionResult<TeamDto>> UploadLogo(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var error = LogoFile.Validate(file);
        if (error is not null)
            return Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);

        var current = await teamService.GetByIdAsync(id, cancellationToken);

        await using var stream = file.OpenReadStream();

        string logoUrl;
        try
        {
            logoUrl = await logoStorage.SaveAsync(stream, LogoFile.ExtensionFor(file.ContentType), cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Диск/каталог недоступны — отвечаем понятным текстом, а не дефолтной 500-страницей.
            return Problem(
                detail: "Не удалось сохранить файл логотипа на сервере. Проверьте права на каталог wwwroot/uploads.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var team = await teamService.SetLogoAsync(id, logoUrl, cancellationToken);

        // Старый файл больше не нужен: в БД уже новый путь.
        await logoStorage.DeleteAsync(current.LogoUrl, cancellationToken);

        return Ok(team);
    }

    [HttpDelete("{id:guid}/logo")]
    public async Task<ActionResult<TeamDto>> DeleteLogo(Guid id, CancellationToken cancellationToken)
    {
        var current = await teamService.GetByIdAsync(id, cancellationToken);
        var team = await teamService.SetLogoAsync(id, null, cancellationToken);

        await logoStorage.DeleteAsync(current.LogoUrl, cancellationToken);

        return Ok(team);
    }
}
