using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Domain.Entities;

public sealed class League : AggregateRoot
{
    public Guid OrganizerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public Sport Sport { get; private set; }
    public string? City { get; private set; }
    public string? Region { get; private set; }
    public LeagueDataSource DataSource { get; private set; }
    public LeagueStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private League()
    {
    }

    private League(
        Guid id,
        Guid organizerId,
        string name,
        string slug,
        Sport sport,
        string? city,
        string? region,
        LeagueDataSource dataSource)
    {
        Id = id;
        OrganizerId = organizerId;
        Name = name;
        Slug = slug;
        Sport = sport;
        City = city;
        Region = region;
        DataSource = dataSource;
        Status = LeagueStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static League Create(
        Guid organizerId,
        string name,
        string slug,
        Sport sport,
        string? city = null,
        string? region = null,
        LeagueDataSource dataSource = LeagueDataSource.Native)
    {
        if (organizerId == Guid.Empty)
            throw new DomainException("Organizer id обязательное поле.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("League name обязательное поле.");

        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainException("League slug обязательное поле.");

        if (!Enum.IsDefined(sport))
            throw new DomainException($"Неизвестный вид спорта '{sport}'.");

        if (!Enum.IsDefined(dataSource))
            throw new DomainException($"Неизвестный источник данных '{dataSource}'.");

        return new League(
            Guid.NewGuid(),
            organizerId,
            name.Trim(),
            slug.Trim().ToLowerInvariant(),
            sport,
            NormalizeOptional(city),
            NormalizeOptional(region),
            dataSource);
    }

    public void Publish()
    {
        if (Status != LeagueStatus.Draft)
            throw new ConflictException($"Только лига в статусе {LeagueStatus.Draft} может быть опубликована.");

        // Без города лига не попадёт в поиск "лиги рядом со мной"
        if (City is null)
            throw new DomainException("Город обязателен для публикации лиги.");

        Status = LeagueStatus.Published;
    }

    public void Archive()
    {
        if (Status == LeagueStatus.Archived)
            throw new ConflictException("Лига уже архивирована.");

        Status = LeagueStatus.Archived;
    }

    public void EnsureCanAddTournament()
    {
        if (Status == LeagueStatus.Archived)
            throw new ConflictException("Нельзя добавить турнир в архивированную лигу.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
