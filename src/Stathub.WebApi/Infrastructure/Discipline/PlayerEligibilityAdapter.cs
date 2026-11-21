using Stathub.Modules.Discipline.Application;
using Stathub.Modules.Matches.Application.Interfaces;

namespace Stathub.WebApi.Infrastructure.Discipline;

/// <summary>
/// Адаптер «допуск игрока → дисциплинарный модуль и заявки». Живёт в WebApi, потому что связывает
/// Application нескольких модулей и не добавляет зависимость внутрь Matches.Infrastructure.
/// </summary>
internal sealed class PlayerEligibilityAdapter(
    DisciplineService disciplineService,
    IPlayerRosterHistory rosterHistory) : IPlayerEligibility
{
    public Task<int?> GetBanLeftAsync(
        Guid tournamentId,
        Guid playerId,
        CancellationToken cancellationToken = default) =>
        disciplineService.GetBanLeftAsync(tournamentId, playerId, cancellationToken);

    public async Task<bool> IsRegisteredAsync(
        Guid tournamentId,
        Guid teamId,
        Guid playerId,
        DateOnly onDate,
        CancellationToken cancellationToken = default)
    {
        var stints = await rosterHistory.GetStintsAsync(tournamentId, playerId, cancellationToken);

        // День ухода уже не день старой команды: при трансфере он же — первый день в новой.
        return stints.Any(stint =>
            stint.TeamId == teamId
            && stint.JoinedOn <= onDate
            && (stint.LeftOn is null || stint.LeftOn > onDate));
    }
}
