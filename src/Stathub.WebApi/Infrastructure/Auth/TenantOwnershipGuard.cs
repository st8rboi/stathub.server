using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Modules.Leagues.Application;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Shared.Exceptions;

namespace Stathub.WebApi.Infrastructure.Auth;

/// <summary>
/// Проверка «этот объект принадлежит организации из токена». Нужна из-за того, что Id приходят
/// из URL: без неё организатор мог бы открыть чужую лигу, зная её идентификатор.
/// Чужой объект отдаём как 404 — не подтверждаем даже факт его существования.
/// </summary>
public sealed class TenantOwnershipGuard(
    LeagueService leagueService,
    ITournamentReader tournamentReader,
    IStagePublicReader stagePublicReader,
    IDivisionReader divisionReader,
    ICurrentUser currentUser)
{
    public async Task EnsureLeagueAsync(Guid leagueId, CancellationToken cancellationToken = default)
    {
        var league = await leagueService.GetByIdAsync(leagueId, cancellationToken);

        if (league.OrganizerId != currentUser.TenantId)
            throw new NotFoundException($"Лига '{leagueId}' не найдена.");
    }

    public async Task EnsureTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var tournament = await tournamentReader.GetAsync(tournamentId, cancellationToken)
            ?? throw new NotFoundException($"Турнир '{tournamentId}' не найден.");

        await EnsureLeagueAsync(tournament.LeagueId, cancellationToken);
    }

    /// <summary>Дивизион → турнир → лига: проверка владения для маршрутов `/divisions/{divisionId}/...`.</summary>
    public async Task EnsureDivisionAsync(Guid divisionId, CancellationToken cancellationToken = default)
    {
        var division = await divisionReader.GetAsync(divisionId, cancellationToken)
            ?? throw new NotFoundException($"Дивизион '{divisionId}' не найден.");

        await EnsureTournamentAsync(division.TournamentId, cancellationToken);
    }

    /// <summary>Этап → турнир → лига: проверка владения для маршрутов `/stages/{stageId}/...`.</summary>
    public async Task EnsureStageAsync(Guid stageId, CancellationToken cancellationToken = default)
    {
        var stage = await stagePublicReader.GetAsync(stageId, cancellationToken)
            ?? throw new NotFoundException($"Этап '{stageId}' не найден.");

        await EnsureTournamentAsync(stage.TournamentId, cancellationToken);
    }
}
