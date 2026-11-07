using Stathub.Modules.Identity.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Identity.Domain.Entities;

/// <summary>
/// Организация (тенант): то, что на уровне домена называется организатором лиг.
/// На её Id ссылается <c>League.OrganizerId</c> без cross-module FK (AGENTS.md, правило 3).
/// </summary>
public sealed class Tenant : AggregateRoot
{
    private const int NameMaxLength = 150;
    private const int SlugMaxLength = 64;

    public string Name { get; private set; } = string.Empty;

    /// <summary>Короткое имя для URL/логов: `lflperm`.</summary>
    public string Slug { get; private set; } = string.Empty;

    public TenantStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private Tenant()
    {
    }

    private Tenant(Guid id, string name, string slug)
    {
        Id = id;
        Name = name;
        Slug = slug;
        Status = TenantStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Tenant Create(string name, string slug)
    {
        var normalizedName = (name ?? string.Empty).Trim();
        if (normalizedName.Length == 0)
            throw new DomainException("Название организации обязательно.");
        if (normalizedName.Length > NameMaxLength)
            throw new DomainException($"Название организации не может быть длиннее {NameMaxLength} символов.");

        var normalizedSlug = NormalizeSlug(slug, normalizedName);

        return new Tenant(Guid.NewGuid(), normalizedName, normalizedSlug);
    }

    public void Rename(string name)
    {
        var normalized = (name ?? string.Empty).Trim();
        if (normalized.Length == 0)
            throw new DomainException("Название организации обязательно.");

        Name = normalized;
    }

    public void Suspend() => Status = TenantStatus.Suspended;

    public void Activate() => Status = TenantStatus.Active;

    /// <summary>Slug из явного значения или транслитерации названия (кириллица → латиница).</summary>
    public static string NormalizeSlug(string? slug, string fallbackName)
    {
        var source = string.IsNullOrWhiteSpace(slug) ? fallbackName : slug;
        var builder = new System.Text.StringBuilder(source.Length);

        foreach (var symbol in source.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(symbol))
            {
                builder.Append(symbol);
                continue;
            }

            if (Transliteration.TryGetValue(symbol, out var latin))
                builder.Append(latin);
            else if (symbol is ' ' or '-' or '_')
                builder.Append('-');
        }

        var normalized = builder.ToString().Trim('-');
        while (normalized.Contains("--", StringComparison.Ordinal))
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);

        if (normalized.Length == 0)
            normalized = $"tenant-{Guid.NewGuid():N}"[..16];
        if (normalized.Length > SlugMaxLength)
            normalized = normalized[..SlugMaxLength].TrimEnd('-');

        return normalized;
    }

    private static readonly Dictionary<char, string> Transliteration = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e",
        ['ж'] = "zh", ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m",
        ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
        ['ф'] = "f", ['х'] = "h", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "",
        ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya"
    };
}
