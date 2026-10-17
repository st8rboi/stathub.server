using Microsoft.AspNetCore.Mvc;
using Stathub.Modules.Players.Application;
using Stathub.Modules.Players.Application.Dtos;

namespace Stathub.WebApi.Controllers.Players;

[ApiController]
[Route("players")]
public sealed class PlayersController(PlayerService playerService) : ControllerBase
{
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
