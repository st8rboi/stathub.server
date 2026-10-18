using Stathub.Modules.Discipline.Application;
using Stathub.Modules.Matches.Application.Interfaces;

namespace Stathub.WebApi.Infrastructure.Discipline;

/// <summary>
/// Адаптер «допуск игрока → дисциплинарный модуль». Живёт в WebApi, потому что связывает
/// Application двух модулей и не добавляет зависимость внутрь Matches.Infrastructure.
/// </summary>
internal sealed class PlayerEligibilityAdapter(DisciplineService disciplineService) : IPlayerEligibility
{
    public Task<int?> GetBanLeftAsync(
        Guid tournamentId,
        Guid teamId,
        Guid playerId,
        CancellationToken cancellationToken = default) =>
        disciplineService.GetBanLeftAsync(tournamentId, teamId, playerId, DateTime.UtcNow, cancellationToken);
}
