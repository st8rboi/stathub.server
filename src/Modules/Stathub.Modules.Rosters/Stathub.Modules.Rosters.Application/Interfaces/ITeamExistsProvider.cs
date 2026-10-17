namespace Stathub.Modules.Rosters.Application.Interfaces;

/// <summary>Порт потребителя: существует ли команда (реализация — поверх ITeamReader модуля Teams).</summary>
public interface ITeamExistsProvider
{
    Task<bool> ExistsAsync(Guid teamId, CancellationToken cancellationToken = default);
}
