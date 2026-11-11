using Stathub.Modules.Players.Application.Dtos;
using Stathub.Modules.Players.Application.Interfaces;
using Stathub.Modules.Players.Domain.Entities;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Players.Application;

public sealed class PlayerService(
    IPlayerRepository repository,
    IEnumerable<IPlayerUsageSource> usageSources)
{
    public const int DefaultSearchLimit = 10;
    public const int MaxSearchLimit = 50;

    /// <summary>Сколько игроков показывать в админке по умолчанию.</summary>
    public const int DefaultListLimit = 20;
    public const int MaxListLimit = 100;

    public async Task<PlayerDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        PlayerDto.From(await GetOrThrowAsync(id, cancellationToken));

    /// <summary>
    /// Создаёт игрока. Дубликаты по ФИО не блокируются: полные тёзки — обычное дело,
    /// а автоматическое слияние запрещено (documentation/doc.md §9.3). Организатору
    /// показываются подсказки существующих игроков, решение принимает человек.
    /// </summary>
    public async Task<Guid> CreateAsync(
        string fullName,
        DateOnly? birthDate = null,
        CancellationToken cancellationToken = default)
    {
        var player = Player.Create(fullName, birthDate);

        await repository.AddAsync(player, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return player.Id;
    }

    /// <summary>
    /// Список игроков для админки: без запроса — недавно добавленные, с запросом — поиск
    /// тем же механизмом триграмм, что и подсказки в заявке.
    /// </summary>
    public async Task<IReadOnlyList<PlayerDto>> ListAsync(
        string? query,
        int limit = DefaultListLimit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxListLimit)
            throw new DomainException($"Количество игроков должно быть от 1 до {MaxListLimit}.");

        if (string.IsNullOrWhiteSpace(query))
        {
            var recent = await repository.ListRecentAsync(limit, cancellationToken);
            return recent.Select(PlayerDto.From).ToList();
        }

        var found = await repository.FindByNameAsync(query.Trim(), limit, cancellationToken);
        return found.Select(PlayerDto.From).ToList();
    }

    /// <summary>
    /// Игроки по набору Id — для отображения составов: заявки хранят только Id игрока,
    /// а имена живут в этом модуле.
    /// </summary>
    public async Task<IReadOnlyList<PlayerDto>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];

        if (ids.Count > MaxListLimit)
            throw new DomainException($"За один запрос можно получить не более {MaxListLimit} игроков.");

        var players = await repository.GetByIdsAsync(ids, cancellationToken);
        return players.Select(PlayerDto.From).ToList();
    }

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

    /// <summary>
    /// Исправление ФИО и даты рождения. Игрок общий для всех организаторов, поэтому, как и логотип
    /// команды, его правит только тот, в чьих лигах он один используется (см. <see cref="Player.Update"/>).
    /// </summary>
    public async Task<PlayerDto> UpdateAsync(
        Guid id,
        string fullName,
        DateOnly? birthDate,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var player = await repository.GetForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Игрок '{id}' не найден.");

        var usages = await GetUsagesAsync(id, cancellationToken);
        var organizerIds = usages.SelectMany(usage => usage.OrganizerIds).Distinct().ToList();

        player.Update(fullName, birthDate, tenantId, organizerIds);
        await repository.SaveChangesAsync(cancellationToken);

        return PlayerDto.From(player);
    }

    /// <summary>
    /// Удаление игрока, на которого ещё никто не ссылается (ни заявки, ни события матчей).
    /// Между модулями нет FK, поэтому удаление использованного игрока оставило бы
    /// составы и протоколы с несуществующим человеком. Возвращает путь к фото, чтобы
    /// вызывающий удалил файл из хранилища.
    /// </summary>
    public async Task<string?> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var player = await repository.GetForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Игрок '{id}' не найден.");

        var usages = await GetUsagesAsync(id, cancellationToken);
        if (usages.Count > 0)
            throw new ConflictException(
                $"Игрока нельзя удалить: он используется ({string.Join(", ", usages.Select(usage => usage.Description))}).");

        var photoUrl = player.PhotoUrl;

        repository.Remove(player);
        await repository.SaveChangesAsync(cancellationToken);

        return photoUrl;
    }

    /// <summary>
    /// Игрок, если организации разрешено менять его фото. Проверка до сохранения файла:
    /// иначе при отказе в хранилище останется файл, на который никто не ссылается.
    /// </summary>
    public async Task<PlayerDto> GetForPhotoChangeAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var player = await GetOrThrowAsync(id, cancellationToken);
        player.EnsureCanChangePhoto(tenantId);

        return PlayerDto.From(player);
    }

    /// <summary>Сохраняет путь к загруженному фото (null — сбросить).</summary>
    public async Task<PlayerDto> SetPhotoAsync(
        Guid id,
        string? photoUrl,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // Читаем с отслеживанием: иначе EF не заметит изменение и SaveChanges ничего не запишет.
        var player = await repository.GetForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Игрок '{id}' не найден.");

        player.SetPhoto(photoUrl, tenantId);
        await repository.SaveChangesAsync(cancellationToken);

        return PlayerDto.From(player);
    }

    /// <summary>Сброс фото техподдержкой (см. PlayerOps). Возвращает путь к старому файлу.</summary>
    public async Task<string?> ResetPhotoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var player = await repository.GetForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Игрок '{id}' не найден.");

        var previousPhotoUrl = player.PhotoUrl;

        player.ResetPhoto();
        await repository.SaveChangesAsync(cancellationToken);

        return previousPhotoUrl;
    }

    private async Task<IReadOnlyList<PlayerUsageDto>> GetUsagesAsync(Guid id, CancellationToken cancellationToken)
    {
        var usages = new List<PlayerUsageDto>();

        // Последовательно: параллельные запросы в одном DbContext EF не поддерживает.
        foreach (var source in usageSources)
        {
            var usage = await source.GetUsageAsync(id, cancellationToken);
            if (usage is not null)
                usages.Add(usage);
        }

        return usages;
    }

    private async Task<Player> GetOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Игрок '{id}' не найден.");
}
