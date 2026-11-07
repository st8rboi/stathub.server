using Stathub.Modules.Identity.Application;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Modules.Identity.Domain.Enums;
using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Matches.Application;
using Stathub.Shared.Exceptions;

namespace Stathub.WebApi.Infrastructure.Auth;

/// <summary>
/// Доступ к матчу: организатор/админ — в пределах своей организации, судья — только
/// на назначенный ему матч. Проверка идёт по БД на каждый запрос, а не по claims,
/// поэтому отзыв назначения действует сразу.
/// </summary>
public sealed class MatchAccessGuard(
    MatchService matchService,
    AccountService accountService,
    TenantOwnershipGuard ownershipGuard,
    IStagePublicReader stagePublicReader,
    ICurrentUser currentUser)
{
    public async Task EnsureMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await matchService.GetByIdAsync(matchId, cancellationToken);

        if (currentUser.Role is UserRole.Organizer or UserRole.Admin)
        {
            await ownershipGuard.EnsureTournamentAsync(match.TournamentId, cancellationToken);
            return;
        }

        var assignedMatches = await accountService.GetAssignedMatchIdsAsync(
            currentUser.UserId,
            currentUser.TenantId,
            cancellationToken);

        if (!assignedMatches.Contains(matchId))
            throw new ForbiddenException("Этот матч вам не назначен.");
    }

    /// <summary>
    /// Весь этап доступен только организатору своей лиги. Остальным роль закрывает доступ:
    /// судья получает лишь отфильтрованный список назначенных матчей (см. MatchesController).
    /// </summary>
    public async Task EnsureStageAsync(Guid stageId, CancellationToken cancellationToken = default)
    {
        if (currentUser.Role is not (UserRole.Organizer or UserRole.Admin))
            throw new ForbiddenException("Этот этап вам недоступен.");

        var stage = await stagePublicReader.GetAsync(stageId, cancellationToken)
            ?? throw new NotFoundException($"Этап '{stageId}' не найден.");

        await ownershipGuard.EnsureTournamentAsync(stage.TournamentId, cancellationToken);
    }

    public Task<StagePublicInfoDto?> FindStageAsync(Guid stageId, CancellationToken cancellationToken = default) =>
        stagePublicReader.GetAsync(stageId, cancellationToken);

    /// <summary>
    /// Доступ к заявкам этапа: организатор — по владению лигой, судья — если у него есть
    /// назначенный матч на этом этапе (состав нужен для отметки автора гола).
    /// </summary>
    public async Task EnsureStageRosterAsync(Guid stageId, CancellationToken cancellationToken = default)
    {
        if (currentUser.Role is UserRole.Organizer or UserRole.Admin)
        {
            await ownershipGuard.EnsureStageAsync(stageId, cancellationToken);
            return;
        }

        var assignedMatches = await accountService.GetAssignedMatchIdsAsync(
            currentUser.UserId,
            currentUser.TenantId,
            cancellationToken);

        if (assignedMatches.Count == 0)
            throw new ForbiddenException("У вас нет назначенных матчей.");

        var matches = await matchService.GetByIdsAsync(assignedMatches, cancellationToken);

        if (matches.All(match => match.StageId != stageId))
            throw new ForbiddenException("Этот этап вам не назначен.");
    }
}
