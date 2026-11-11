using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Players.Domain.Entities;

/// <summary>
/// Игрок — сущность идентичности: реальный человек, который может играть за разные команды
/// в разных лигах разных организаторов.
/// <para>
/// ВАЖНО: у игрока намеренно НЕТ TenantId — это единственное исключение из правила
/// «каждая таблица домена скоуплена по тенанту». ФИО и дата рождения ничего не знают
/// про организатора, лигу или правила. Вся тенант-специфичная информация (за кого играет,
/// статистика, дисциплинарная история) живёт в PlayerRegistration, а не здесь.
/// См. documentation/doc.md §7 и §9.3.
/// </para>
/// </summary>
public sealed class Player : AggregateRoot
{
    private const int MaxAgeYears = 120;

    public string FullName { get; private set; } = string.Empty;

    // Дата рождения необязательна: организатор может её не знать.
    // Но она — главный различитель полных тёзок, поэтому показывается в подсказках поиска.
    public DateOnly? BirthDate { get; private set; }

    /// <summary>
    /// Относительный путь к загруженному фото (например, <c>/uploads/logos/{id}.jpg</c>).
    /// Файл живёт в объектном хранилище, в БД — только путь.
    /// </summary>
    public string? PhotoUrl { get; private set; }

    /// <summary>
    /// Организация, загрузившая фото. Игрок общий для всех лиг, поэтому фото, как и логотип команды,
    /// закрепляется за первым загрузившим: остальные его переиспользуют или меняют через техподдержку.
    /// </summary>
    public Guid? PhotoOwnerTenantId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private Player()
    {
    }

    private Player(Guid id, string fullName, DateOnly? birthDate)
    {
        Id = id;
        FullName = fullName;
        BirthDate = birthDate;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Player Create(string fullName, DateOnly? birthDate = null)
    {
        var normalizedName = NormalizeName(fullName);

        if (birthDate is not null)
            ValidateBirthDate(birthDate.Value);

        return new Player(Guid.NewGuid(), normalizedName, birthDate);
    }

    /// <summary>
    /// Исправление ФИО и даты рождения. Игрок общий для всех организаторов, поэтому, как и
    /// логотип команды, править его может только организатор, в чьих лигах он один используется;
    /// иначе — через техподдержку. <paramref name="usedByOrganizerIds"/> — организаторы,
    /// в турнирах которых игрок заявлен или участвовал в событиях матчей.
    /// </summary>
    public void Update(
        string fullName,
        DateOnly? birthDate,
        Guid tenantId,
        IReadOnlyCollection<Guid> usedByOrganizerIds)
    {
        EnsureCanEdit(tenantId, usedByOrganizerIds);

        var normalizedName = NormalizeName(fullName);

        if (birthDate is not null)
            ValidateBirthDate(birthDate.Value);

        FullName = normalizedName;
        BirthDate = birthDate;
    }

    /// <summary>Править можно игрока, который не используется в лигах других организаторов.</summary>
    public static void EnsureCanEdit(Guid tenantId, IReadOnlyCollection<Guid> usedByOrganizerIds)
    {
        if (usedByOrganizerIds.Any(organizerId => organizerId != tenantId))
            throw new ForbiddenException(
                "Игрок используется в лигах других организаторов. Изменить его можно через техподдержку.");
    }

    /// <summary>Фото без владельца свободно; занятое может менять только его владелец.</summary>
    public void EnsureCanChangePhoto(Guid tenantId)
    {
        if (PhotoUrl is not null && PhotoOwnerTenantId != tenantId)
            throw new ForbiddenException(
                "Фото игрока уже загружено другим организатором. Заменить его можно через техподдержку.");
    }

    /// <summary>
    /// Устанавливает или сбрасывает фото (<paramref name="photoUrl"/> = null — удалить,
    /// после чего фото снова может загрузить любой организатор).
    /// </summary>
    public void SetPhoto(string? photoUrl, Guid tenantId)
    {
        EnsureCanChangePhoto(tenantId);

        var normalized = string.IsNullOrWhiteSpace(photoUrl) ? null : photoUrl.Trim();

        if (normalized is { Length: > 500 })
            throw new DomainException("Путь к фото не может быть длиннее 500 символов.");

        PhotoUrl = normalized;
        PhotoOwnerTenantId = normalized is null ? null : tenantId;
    }

    /// <summary>Сброс фото техподдержкой: снимает закрепление за организацией.</summary>
    public void ResetPhoto()
    {
        PhotoUrl = null;
        PhotoOwnerTenantId = null;
    }

    private static string NormalizeName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("ФИО игрока обязательное поле.");

        var normalizedName = fullName.Trim();

        if (normalizedName.Length > 200)
            throw new DomainException("ФИО игрока не может быть длиннее 200 символов.");

        return normalizedName;
    }

    /// <summary>
    /// Проверяет, что игрок не рождён в будущем и возраст не превышает разумный предел.
    /// Нужно, чтобы опечатка «1926» вместо «2006» не создавала молча несуществующего игрока.
    /// </summary>
    private static void ValidateBirthDate(DateOnly birthDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (birthDate > today)
            throw new DomainException("Дата рождения игрока не может быть в будущем.");

        if (birthDate < today.AddYears(-MaxAgeYears))
            throw new DomainException($"Возраст игрока не может превышать {MaxAgeYears} лет.");
    }
}
