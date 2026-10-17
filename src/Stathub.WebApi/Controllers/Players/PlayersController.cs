using Microsoft.AspNetCore.Mvc;
using Stathub.Modules.Players.Application;
using Stathub.Modules.Players.Application.Dtos;
using Stathub.WebApi.Contracts.Players;

namespace Stathub.WebApi.Controllers.Players;

[ApiController]
[Route("players")]
public sealed class PlayersController(PlayerService playerService) : ControllerBase
{
    [HttpPost]
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
