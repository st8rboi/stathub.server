using Stathub.Modules.Players.Application.Dtos;
using Stathub.Modules.Players.Application.Interfaces;
using Stathub.Modules.Players.Domain.Entities;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Players.Application;

public sealed class PlayerService(IPlayerRepository repository)
{
    public const int DefaultSearchLimit = 10;
    public const int MaxSearchLimit = 50;

    public async Task<PlayerDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        PlayerDto.From(await GetOrThrowAsync(id, cancellationToken));

    /// <summary>
    /// Подсказки существующих игроков для typeahead-поиска.
    /// </summary>
    /// <remarks>
    /// Пустой запрос не ищем: без строки подсказка вернула бы произвольных игроков,
    /// а по §9.3 человеку нужен осмысленный список для сравнения с вводимым ФИО.
    /// </remarks>
    public async Task<IReadOnlyList<PlayerSuggestionDto>> SearchAsync(
        string? query,
        int limit = DefaultSearchLimit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxSearchLimit)
            throw new DomainException($"Количество подсказок должно быть от 1 до {MaxSearchLimit}.");

        if (string.IsNullOrWhiteSpace(query))
            return [];

        // Триграммы в Postgres регистронезависимы, поэтому отдельная нормализация регистра не нужна.
        var players = await repository.FindByNameAsync(query.Trim(), limit, cancellationToken);

        return players.Select(PlayerSuggestionDto.From).ToList();
    }

    private async Task<Player> GetOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Игрок '{id}' не найден.");
}
