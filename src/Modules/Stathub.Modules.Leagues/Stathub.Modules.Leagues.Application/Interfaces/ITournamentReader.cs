using Stathub.Modules.Leagues.Application.Dtos;

namespace Stathub.Modules.Leagues.Application.Interfaces;

/// <summary>
/// Чтение статуса турнира потребителями (модуль заявок проверяет, можно ли менять состав;
/// витрина — можно ли показывать турнир). Объявлен в модуле-поставщике,
/// реализация — в Leagues.Infrastructure.
/// </summary>
public interface ITournamentReader
{
    Task<TournamentStatusInfoDto?> GetAsync(Guid tournamentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Организаторы (владельцы лиг) указанных турниров, без повторов. Нужно справочникам
    /// команд и игроков: общую запись может править только организатор, в чьих лигах она одна используется.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetOrganizerIdsAsync(
        IReadOnlyCollection<Guid> tournamentIds,
        CancellationToken cancellationToken = default);

    /// <summary>Данные турнира для витрины: имя, лига, логотип и признак публикации.</summary>
    Task<TournamentPublicInfoDto?> GetPublicInfoAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);
}
