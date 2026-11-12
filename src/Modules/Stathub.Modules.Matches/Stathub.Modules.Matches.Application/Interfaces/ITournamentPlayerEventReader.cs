using Stathub.Modules.Matches.Application.Dtos;

namespace Stathub.Modules.Matches.Application.Interfaces;

/// <summary>
/// События игроков дивизиона для витрины статистики (модуль Statistics).
/// Граница — дивизион, а не турнир: дивизионы — независимые соревнования со своими бомбардирами.
/// Объявлен в модуле-поставщике, реализация — в Matches.Infrastructure.
/// </summary>
public interface ITournamentPlayerEventReader
{
    /// <summary>
    /// Значимые события (голы, автоголы, карточки) с указанным игроком, кроме отменённых.
    /// </summary>
    Task<IReadOnlyList<TournamentPlayerEventDto>> GetByDivisionAsync(
        Guid divisionId,
        CancellationToken cancellationToken = default);
}
