using Stathub.Modules.Leagues.Application.Dtos;

namespace Stathub.Modules.Leagues.Application.Interfaces;

/// <summary>
/// Чтение дивизионов потребителями: заявки привязаны к дивизиону, статистика считается по нему.
/// Объявлен в модуле-поставщике, реализация — в Leagues.Infrastructure.
/// </summary>
public interface IDivisionReader
{
    Task<DivisionInfoDto?> GetAsync(Guid divisionId, CancellationToken cancellationToken = default);

    /// <summary>Дивизионы турнира по порядку.</summary>
    Task<IReadOnlyList<DivisionInfoDto>> ListByTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);
}
