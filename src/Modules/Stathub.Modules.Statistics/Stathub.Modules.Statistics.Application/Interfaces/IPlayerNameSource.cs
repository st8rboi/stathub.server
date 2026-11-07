namespace Stathub.Modules.Statistics.Application.Interfaces;

/// <summary>ФИО игроков для строк статистики (реализация — поверх модуля Players).</summary>
public interface IPlayerNameSource
{
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> playerIds,
        CancellationToken cancellationToken = default);
}
