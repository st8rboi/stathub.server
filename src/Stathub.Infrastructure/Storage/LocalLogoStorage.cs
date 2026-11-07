namespace Stathub.Infrastructure.Storage;

/// <summary>
/// Локальное хранилище логотипов: файлы лежат в <c>{uploadsRoot}/logos</c>, раздаются
/// статикой по <c>/uploads/...</c>. Путь в БД относительный, поэтому перенос в MinIO
/// не потребует менять данные — только реализацию и, при необходимости, миграцию URL.
/// </summary>
public sealed class LocalLogoStorage : ILogoStorage
{
    private const string UrlPrefix = "/uploads/logos";
    private readonly string _logosPath;

    public LocalLogoStorage(string uploadsRootPath)
    {
        _logosPath = Path.Combine(uploadsRootPath, "logos");
        Directory.CreateDirectory(_logosPath);
    }

    public async Task<string> SaveAsync(
        Stream content,
        string extension,
        CancellationToken cancellationToken = default)
    {
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(_logosPath, fileName);

        await using var target = File.Create(filePath);
        await content.CopyToAsync(target, cancellationToken);

        return $"{UrlPrefix}/{fileName}";
    }

    public Task DeleteAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Task.CompletedTask;

        // Удаляем только файлы внутри своего каталога: url приходит из БД, но доверять ему слепо нельзя.
        var fileName = Path.GetFileName(url);
        if (string.IsNullOrWhiteSpace(fileName) || !url.StartsWith(UrlPrefix, StringComparison.Ordinal))
            return Task.CompletedTask;

        var filePath = Path.Combine(_logosPath, fileName);
        if (File.Exists(filePath))
            File.Delete(filePath);

        return Task.CompletedTask;
    }
}
