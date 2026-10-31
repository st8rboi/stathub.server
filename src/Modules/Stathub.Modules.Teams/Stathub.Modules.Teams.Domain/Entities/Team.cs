using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Teams.Domain.Entities;

/// <summary>
/// Команда (клуб) — сущность идентичности: существует вне турниров и сезонов,
/// а участие в конкретном турнире оформляется отдельной сущностью (PlayerRegistration /
/// TournamentTeam) по мере реализации модуля заявок.
/// <para>
/// Как и у <c>Player</c>, здесь намеренно нет TenantId: название и город клуба ничего не знают
/// про организатора (documentation/doc.md §7).
/// </para>
/// </summary>
public sealed class Team : AggregateRoot
{
    private const int NameMaxLength = 150;
    private const int CityMaxLength = 100;

    public string Name { get; private set; } = string.Empty;

    /// <summary>Город необязателен: любительская команда может быть без привязки к городу.</summary>
    public string? City { get; private set; }

    /// <summary>
    /// Относительный путь к загруженному логотипу (например, <c>/uploads/logos/{id}.png</c>).
    /// Файл живёт в объектном хранилище, в БД — только путь: логотип не бинарник.
    /// </summary>
    public string? LogoUrl { get; private set; }

    /// <summary>
    /// Организация, загрузившая логотип. Команда общая для всех лиг, поэтому логотип закрепляется
    /// за первым загрузившим: остальные его переиспользуют или меняют через техподдержку.
    /// </summary>
    public Guid? LogoOwnerTenantId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private Team()
    {
    }

    private Team(Guid id, string name, string? city)
    {
        Id = id;
        Name = name;
        City = city;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Team Create(string name, string? city = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Название команды обязательное поле.");

        var normalizedName = name.Trim();
        if (normalizedName.Length > NameMaxLength)
            throw new DomainException($"Название команды не может быть длиннее {NameMaxLength} символов.");

        var normalizedCity = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        if (normalizedCity is { Length: > CityMaxLength })
            throw new DomainException($"Город не может быть длиннее {CityMaxLength} символов.");

        return new Team(Guid.NewGuid(), normalizedName, normalizedCity);
    }

    /// <summary>
    /// Переименование клуба. История матчей не страдает: матчи ссылаются на Id команды,
    /// а не на её название.
    /// </summary>
    public void Rename(string name, string? city)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Название команды обязательное поле.");

        var normalizedName = name.Trim();
        if (normalizedName.Length > NameMaxLength)
            throw new DomainException($"Название команды не может быть длиннее {NameMaxLength} символов.");

        var normalizedCity = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        if (normalizedCity is { Length: > CityMaxLength })
            throw new DomainException($"Город не может быть длиннее {CityMaxLength} символов.");

        Name = normalizedName;
        City = normalizedCity;
    }

    /// <summary>Логотип без владельца свободен; занятый может менять только его владелец.</summary>
    public void EnsureCanChangeLogo(Guid tenantId)
    {
        if (LogoUrl is not null && LogoOwnerTenantId != tenantId)
            throw new ForbiddenException(
                "Логотип команды уже загружен другим организатором. Заменить его можно через техподдержку.");
    }

    /// <summary>
    /// Устанавливает или сбрасывает логотип (<paramref name="logoUrl"/> = null — удалить,
    /// после чего логотип снова может загрузить любой организатор).
    /// </summary>
    public void SetLogo(string? logoUrl, Guid tenantId)
    {
        EnsureCanChangeLogo(tenantId);

        var normalized = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();

        if (normalized is { Length: > 500 })
            throw new DomainException("Путь к логотипу не может быть длиннее 500 символов.");

        LogoUrl = normalized;
        LogoOwnerTenantId = normalized is null ? null : tenantId;
    }

    /// <summary>Сброс логотипа техподдержкой: снимает закрепление за организацией.</summary>
    public void ResetLogo()
    {
        LogoUrl = null;
        LogoOwnerTenantId = null;
    }
}
