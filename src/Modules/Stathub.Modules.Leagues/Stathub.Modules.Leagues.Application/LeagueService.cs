using Stathub.Modules.Leagues.Application.Abstractions;
using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.Modules.Leagues.Domain.Entities;
using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Application;

public sealed class LeagueService(ILeagueRepository repository)
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

    public async Task PublishAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var league = await GetOrThrowAsync(id, cancellationToken);

        league.Publish();
        await repository.SaveChangesAsync(cancellationToken);
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
