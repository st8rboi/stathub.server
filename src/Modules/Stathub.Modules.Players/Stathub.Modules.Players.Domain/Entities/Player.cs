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
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("ФИО игрока обязательное поле.");

        var normalizedName = fullName.Trim();

        if (normalizedName.Length > 200)
            throw new DomainException("ФИО игрока не может быть длиннее 200 символов.");

        if (birthDate is not null)
            ValidateBirthDate(birthDate.Value);

        return new Player(Guid.NewGuid(), normalizedName, birthDate);
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
