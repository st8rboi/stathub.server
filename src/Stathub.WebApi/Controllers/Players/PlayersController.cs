using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stathub.Infrastructure.Storage;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Modules.Players.Application;
using Stathub.Modules.Players.Application.Dtos;
using Stathub.WebApi.Contracts.Players;
using Stathub.WebApi.Infrastructure.Auth;
using Stathub.WebApi.Infrastructure.Uploads;

namespace Stathub.WebApi.Controllers.Players;

[ApiController]
[Route("players")]
// Чтение нужно всем в организации (судья показывает авторов событий),
// создание — только организатору.
[Authorize(Policy = AuthPolicies.TenantMember)]
public sealed class PlayersController(
    PlayerService playerService,
    ILogoStorage logoStorage,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Исправление ФИО и даты рождения. 403 — игрок используется в лигах других организаторов
    /// (правка через техподдержку, как с логотипом команды).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<ActionResult<PlayerDto>> UpdatePlayer(
        Guid id,
        UpdatePlayerRequest request,
        CancellationToken cancellationToken) =>
        Ok(await playerService.UpdateAsync(
            id, request.FullName, request.BirthDate, currentUser.TenantId, cancellationToken));

    /// <summary>Удаление игрока. 409 — игрок уже есть в заявках или событиях матчей.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<IActionResult> DeletePlayer(Guid id, CancellationToken cancellationToken)
    {
        var photoUrl = await playerService.DeleteAsync(id, cancellationToken);

        // Игрока в БД уже нет — файл фото больше никому не нужен.
        await logoStorage.DeleteAsync(photoUrl, cancellationToken);

        return NoContent();
    }

    /// <summary>Загрузка фото игрока: файл кладётся в хранилище, в БД — только путь.</summary>
    [HttpPost("{id:guid}/photo")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    // Лимит выше бизнес-лимита, чтобы размер проверяла наша валидация и отвечала
    // понятным текстом, а не «Request body too large» от формата формы.
    [RequestSizeLimit(LogoFile.MaxRequestBytes)]
    [RejectTooLargeLogo]
    public async Task<ActionResult<PlayerDto>> UploadPhoto(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var error = LogoFile.Validate(file);
        if (error is not null)
            return Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);

        var current = await playerService.GetForPhotoChangeAsync(id, currentUser.TenantId, cancellationToken);

        await using var stream = file.OpenReadStream();

        string photoUrl;
        try
        {
            photoUrl = await logoStorage.SaveAsync(stream, LogoFile.ExtensionFor(file.ContentType), cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Problem(
                detail: "Не удалось сохранить файл фото на сервере. Проверьте права на каталог wwwroot/uploads.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var player = await playerService.SetPhotoAsync(id, photoUrl, currentUser.TenantId, cancellationToken);

        // Старый файл больше не нужен: в БД уже новый путь.
        await logoStorage.DeleteAsync(current.PhotoUrl, cancellationToken);

        return Ok(player);
    }

    [HttpDelete("{id:guid}/photo")]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<ActionResult<PlayerDto>> DeletePhoto(Guid id, CancellationToken cancellationToken)
    {
        var current = await playerService.GetForPhotoChangeAsync(id, currentUser.TenantId, cancellationToken);
        var player = await playerService.SetPhotoAsync(id, null, currentUser.TenantId, cancellationToken);

        await logoStorage.DeleteAsync(current.PhotoUrl, cancellationToken);

        return Ok(player);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.Organizer)]
    public async Task<IActionResult> CreatePlayer(CreatePlayerRequest request, CancellationToken cancellationToken)
    {
        var playerId = await playerService.CreateAsync(request.FullName, request.BirthDate, cancellationToken);

        return CreatedAtAction(
            nameof(GetPlayerById),
            new { id = playerId },
            new { Id = playerId });
    }

    /// <summary>
    /// Список игроков для админки: без <paramref name="query"/> — недавно добавленные,
    /// с запросом — поиск с учётом опечаток. <paramref name="ids"/> — конкретные игроки
    /// по идентификаторам (используется, чтобы показать состав команды).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlayerDto>>> GetPlayers(
        [FromQuery] string? query,
        [FromQuery] string? ids,
        [FromQuery] int limit = PlayerService.DefaultListLimit,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(ids))
        {
            var parsed = ids
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();

            return Ok(await playerService.GetByIdsAsync(parsed, cancellationToken));
        }

        return Ok(await playerService.ListAsync(query, limit, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PlayerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerDto>> GetPlayerById(Guid id, CancellationToken cancellationToken) =>
        Ok(await playerService.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// Подсказки существующих игроков для typeahead-поиска при добавлении в заявку.
    /// Ищет с учётом небольших опечаток. Решение «это тот самый игрок или новый»
    /// принимает человек — автоматического слияния по совпадению ФИО нет.
    /// </summary>
    [HttpGet("suggest")]
    [ProducesResponseType<IReadOnlyList<PlayerSuggestionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PlayerSuggestionDto>>> SuggestPlayers(
        [FromQuery] string? query,
        [FromQuery] int limit = PlayerService.DefaultSearchLimit,
        CancellationToken cancellationToken = default) =>
        Ok(await playerService.SearchAsync(query, limit, cancellationToken));
}
