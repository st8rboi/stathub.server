using Stathub.Modules.Matches.Application.Dtos;

namespace Stathub.Modules.Matches.Application.Interfaces;

/// <summary>
/// События игроков турнира для витрины статистики (модуль Statistics).
/// Объявлен в модуле-поставщике, реализация — в Matches.Infrastructure.
/// </summary>
public interface ITournamentPlayerEventReader
{
    /// <summary>
    /// Значимые события (голы, автоголы, карточки) с указанным игроком, кроме отменённых.
    /// Матчи без турнира (созданные до его появления) в выборку не попадают.
    /// </summary>
    Task<IReadOnlyList<TournamentPlayerEventDto>> GetAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);
}
