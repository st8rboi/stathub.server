using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.Modules.Leagues.Domain.Entities;
using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Application;

public sealed class LeagueService(ILeagueRepository repository, ITournamentRepository tournamentRepository)
{
    public async Task<Guid> CreateLeagueAsync(
        Guid organizerId,
        string name,
        string slug,
        Sport sport,
        string? city,
        string? region,
        LeagueDataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        var league = League.Create(organizerId, name, slug, sport, city, region, dataSource);

        if (await repository.GetBySlugAsync(league.Slug, cancellationToken) is not null)
            throw new ConflictException($"Лига со slug '{league.Slug}' уже существует.");

        await repository.AddAsync(league, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return league.Id;
    }

    public async Task<LeagueDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        LeagueDto.From(await GetOrThrowAsync(id, cancellationToken));

    public async Task<LeagueDto> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var league = await repository.GetBySlugAsync(slug.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new NotFoundException($"Лига со slug '{slug}' не найдена.");

        return LeagueDto.From(league);
    }

    // Без organizerId — публичная витрина (только Published), с organizerId — все лиги организатора, включая Draft
    public async Task<IReadOnlyList<LeagueDto>> ListAsync(
        Guid? organizerId,
        CancellationToken cancellationToken = default)
    {
        LeagueStatus? status = organizerId is null ? LeagueStatus.Published : null;
        var leagues = await repository.ListAsync(organizerId, status, cancellationToken);

        return leagues.Select(LeagueDto.From).ToList();
    }

    /// <summary>
    /// Публичный каталог лиг с фильтрами «лиги в моём городе» и поиском по названию
    /// (documentation/doc.md §2.1). Отдаёт только опубликованные лиги — то же правило,
    /// что и у <see cref="ListAsync"/> без организатора.
    /// </summary>
    public async Task<IReadOnlyList<LeagueDto>> ListPublishedAsync(
        string? query,
        string? city,
        CancellationToken cancellationToken = default)
    {
        var leagues = await repository.ListAsync(null, LeagueStatus.Published, cancellationToken);

        IEnumerable<League> filtered = leagues;

        if (!string.IsNullOrWhiteSpace(city))
        {
            var normalizedCity = city.Trim();
            filtered = filtered.Where(league =>
                league.City is not null &&
                string.Equals(league.City.Trim(), normalizedCity, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalizedQuery = query.Trim();
            filtered = filtered.Where(league =>
                league.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.Select(LeagueDto.From).ToList();
    }

    public async Task PublishAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var league = await GetOrThrowAsync(id, cancellationToken);

        league.Publish();
        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Сохраняет путь к загруженному логотипу лиги (null — сбросить).</summary>
    public async Task<LeagueDto> SetLogoAsync(
        Guid id,
        string? logoUrl,
        CancellationToken cancellationToken = default)
    {
        var league = await GetOrThrowAsync(id, cancellationToken);

        league.SetLogo(logoUrl);
        await repository.SaveChangesAsync(cancellationToken);

        return LeagueDto.From(league);
    }

    /// <summary>Изменение названия, slug, города и региона лиги.</summary>
    public async Task<LeagueDto> UpdateAsync(
        Guid id,
        string name,
        string slug,
        string? city,
        string? region,
        CancellationToken cancellationToken = default)
    {
        var league = await GetOrThrowAsync(id, cancellationToken);

        league.Update(name, slug, city, region);

        // Slug — часть публичного адреса лиги, он уникален среди всех организаторов.
        var sameSlug = await repository.GetBySlugAsync(league.Slug, cancellationToken);
        if (sameSlug is not null && sameSlug.Id != league.Id)
            throw new ConflictException($"Лига со slug '{league.Slug}' уже существует.");

        await repository.SaveChangesAsync(cancellationToken);

        return LeagueDto.From(league);
    }

    /// <summary>
    /// Удаление пустой лиги (без турниров). Возвращает путь к логотипу, чтобы вызывающий
    /// удалил файл из хранилища.
    /// </summary>
    public async Task<string?> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var league = await GetOrThrowAsync(id, cancellationToken);

        var tournaments = await tournamentRepository.ListByLeagueAsync(id, cancellationToken);
        League.EnsureCanDelete(tournaments.Count);

        var logoUrl = league.LogoUrl;

        repository.Remove(league);
        await repository.SaveChangesAsync(cancellationToken);

        return logoUrl;
    }

    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var league = await GetOrThrowAsync(id, cancellationToken);

        league.Archive();
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<League> GetOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Лига '{id}' не найдена.");
}
