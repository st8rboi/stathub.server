namespace Stathub.Infrastructure.Storage;

/// <summary>
/// Хранилище бинарных файлов (логотипы, позже фото игроков). В БД живёт только URL,
/// сам файл — в объектном хранилище; локальный диск — текущая реализация для dev,
/// MinIO подключается заменой реализации без изменения домена (doc §5).
/// </summary>
public interface ILogoStorage
{
    /// <summary>Сохраняет поток и возвращает относительный URL вида <c>/uploads/logos/{id}.png</c>.</summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default);

    /// <summary>Удаляет файл по URL. Несуществующий файл — не ошибка.</summary>
    Task DeleteAsync(string? url, CancellationToken cancellationToken = default);
}
