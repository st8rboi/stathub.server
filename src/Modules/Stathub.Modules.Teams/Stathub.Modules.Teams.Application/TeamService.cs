using Stathub.Modules.Teams.Application.Dtos;
using Stathub.Modules.Teams.Application.Interfaces;
using Stathub.Modules.Teams.Domain.Entities;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Teams.Application;

/// <summary>
/// Справочник команд: организатор заводит клуб один раз, а матчи ссылаются на его Id.
/// </summary>
public sealed class TeamService(
    ITeamRepository repository,
    IEnumerable<ITeamUsageSource> usageSources)
{
    public const int DefaultListLimit = 100;
    public const int MaxListLimit = 500;

    public async Task<Guid> CreateAsync(
        string name,
        string? city = null,
        CancellationToken cancellationToken = default)
    {
        var team = Team.Create(name, city);

        await repository.AddAsync(team, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return team.Id;
    }

    public async Task<TeamDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        TeamDto.From(await GetOrThrowAsync(id, cancellationToken));

    /// <summary>
    /// Изменение названия и города. Команда общая для всех организаторов, поэтому, как и логотип,
    /// её правит только тот, в чьих лигах она одна используется (см. <see cref="Team.Rename"/>).
    /// </summary>
    public async Task<TeamDto> UpdateAsync(
        Guid id,
        string name,
        string? city,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var team = await repository.GetForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Команда '{id}' не найдена.");

        var usages = await GetUsagesAsync(id, cancellationToken);
        var organizerIds = usages.SelectMany(usage => usage.OrganizerIds).Distinct().ToList();

        team.Rename(name, city, tenantId, organizerIds);
        await repository.SaveChangesAsync(cancellationToken);

        return TeamDto.From(team);
    }

    /// <summary>
    /// Удаление команды, на которую ещё никто не ссылается (ни заявки, ни матчи).
    /// Использованную команду удалить нельзя: между модулями нет FK, и удаление оставило бы
    /// матчи и составы с несуществующей командой. Возвращает путь к логотипу, чтобы
    /// вызывающий удалил файл из хранилища.
    /// </summary>
    public async Task<string?> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var team = await repository.GetForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Команда '{id}' не найдена.");

        var usages = await GetUsagesAsync(id, cancellationToken);
        if (usages.Count > 0)
            throw new ConflictException(
                $"Команду нельзя удалить: она используется ({string.Join(", ", usages.Select(usage => usage.Description))}).");

        var logoUrl = team.LogoUrl;

        repository.Remove(team);
        await repository.SaveChangesAsync(cancellationToken);

        return logoUrl;
    }

    private async Task<IReadOnlyList<TeamUsageDto>> GetUsagesAsync(Guid id, CancellationToken cancellationToken)
    {
        var usages = new List<TeamUsageDto>();

        // Последовательно: параллельные запросы в одном DbContext EF не поддерживает.
        foreach (var source in usageSources)
        {
            var usage = await source.GetUsageAsync(id, cancellationToken);
            if (usage is not null)
                usages.Add(usage);
        }

        return usages;
    }

    /// <summary>
    /// Команда, если организации разрешено менять её логотип. Проверка до сохранения файла:
    /// иначе при отказе в хранилище останется файл, на который никто не ссылается.
    /// </summary>
    public async Task<TeamDto> GetForLogoChangeAsync(
        Guid id,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var team = await GetOrThrowAsync(id, cancellationToken);
        team.EnsureCanChangeLogo(tenantId);

        return TeamDto.From(team);
    }

    /// <summary>Сохраняет путь к загруженному логотипу (null — сбросить).</summary>
    public async Task<TeamDto> SetLogoAsync(
        Guid id,
        string? logoUrl,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // Читаем с отслеживанием: иначе EF не заметит изменение и SaveChanges ничего не запишет.
        var team = await repository.GetForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Команда '{id}' не найдена.");

        team.SetLogo(logoUrl, tenantId);
        await repository.SaveChangesAsync(cancellationToken);

        return TeamDto.From(team);
    }

    /// <summary>Сброс логотипа техподдержкой (см. TeamOps). Возвращает путь к старому файлу.</summary>
    public async Task<string?> ResetLogoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var team = await repository.GetForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Команда '{id}' не найдена.");

        var previousLogoUrl = team.LogoUrl;

        team.ResetLogo();
        await repository.SaveChangesAsync(cancellationToken);

        return previousLogoUrl;
    }

    public async Task<IReadOnlyList<TeamDto>> ListAsync(
        string? query,
        int limit = DefaultListLimit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxListLimit)
            throw new DomainException($"Количество команд должно быть от 1 до {MaxListLimit}.");

        var teams = await repository.ListAsync(
            string.IsNullOrWhiteSpace(query) ? null : query.Trim(),
            limit,
            cancellationToken);

        return teams.Select(TeamDto.From).ToList();
    }

    private async Task<Team> GetOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Команда '{id}' не найдена.");

    /// <summary>
    /// Команды по Id — для показа названий в матчах: контракт матча хранит только
    /// идентификаторы команд, имена живут здесь.
    /// </summary>
    public async Task<IReadOnlyList<TeamDto>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];

        if (ids.Count > MaxListLimit)
            throw new DomainException($"За один запрос можно получить не более {MaxListLimit} команд.");

        var teams = await repository.GetByIdsAsync(ids, cancellationToken);
        return teams.Select(TeamDto.From).ToList();
    }
}
