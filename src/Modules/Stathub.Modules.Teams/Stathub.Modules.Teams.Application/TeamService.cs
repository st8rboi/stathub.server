using Stathub.Modules.Teams.Application.Dtos;
using Stathub.Modules.Teams.Application.Interfaces;
using Stathub.Modules.Teams.Domain.Entities;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Teams.Application;

/// <summary>
/// Справочник команд: организатор заводит клуб один раз, а матчи ссылаются на его Id.
/// </summary>
public sealed class TeamService(ITeamRepository repository)
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
