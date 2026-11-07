namespace Stathub.WebApi.Infrastructure.Uploads;

/// <summary>
/// Проверка загружаемого логотипа: тип из белого списка и размер. Расширение берём
/// из content-type, а не из имени файла — имя приходит от клиента и не является доверенным.
/// </summary>
public static class LogoFile
{
    public const long MaxBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Лимит HTTP-запроса: заметно больше бизнес-лимита, чтобы слишком большой файл
    /// отклоняла наша проверка с человеческим текстом, а не ридер формы с английским.
    /// </summary>
    public const long MaxRequestBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/webp"] = ".webp",
    };

    /// <summary>Возвращает текст ошибки или null, если файл допустим.</summary>
    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return "Файл не выбран.";

        if (!AllowedTypes.ContainsKey(file.ContentType))
            return "Поддерживаются только PNG, JPEG и WebP.";

        if (file.Length > MaxBytes)
            return "Логотип не может быть больше 2 МБ.";

        return null;
    }

    public static string ExtensionFor(string contentType) =>
        AllowedTypes.TryGetValue(contentType, out var extension) ? extension : ".png";
}
