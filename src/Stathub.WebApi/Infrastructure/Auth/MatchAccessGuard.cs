using Stathub.Modules.Identity.Application;
using Stathub.Modules.Identity.Application.Interfaces;
using Stathub.Modules.Identity.Domain.Enums;
using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Matches.Application;
using Stathub.Shared.Exceptions;

namespace Stathub.WebApi.Infrastructure.Auth;

/// <summary>
/// Доступ к матчу: организатор/админ — в пределах своей организации, судья и статистик — только
/// на назначенный им матч. Проверка идёт по БД на каждый запрос, а не по claims,
/// поэтому отзыв назначения действует сразу.
/// </summary>
public sealed class MatchAccessGuard(
    MatchService matchService,
    AccountService accountService,
    TenantOwnershipGuard ownershipGuard,
    IStagePublicReader stagePublicReader,
    ICurrentUser currentUser)
{
    /// <summary>
    /// Проверяет доступ к матчу и возвращает его уровень. Права на протокол определяет назначение
    /// на этот матч, а не роль в токене: судья матча ведёт его полностью, статистик — только статистику.
    /// </summary>
    public async Task<MatchAccess> EnsureMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await matchService.GetByIdAsync(matchId, cancellationToken);

        if (currentUser.Role is UserRole.Organizer or UserRole.Admin)
        {
            await ownershipGuard.EnsureTournamentAsync(match.TournamentId, cancellationToken);
            return MatchAccess.Full;
        }

        var roles = await accountService.GetMatchRolesAsync(
            currentUser.UserId,
            currentUser.TenantId,
            matchId,
            cancellationToken);

        if (roles.Count == 0)
            throw new ForbiddenException("Этот матч вам не назначен.");

        return roles.All(role => role == UserRole.Statistician)
            ? MatchAccess.StatisticsOnly
            : MatchAccess.Full;
    }

    /// <summary>
    /// Весь этап доступен только организатору своей лиги. Остальным роль закрывает доступ:
    /// судья и статистик получают лишь отфильтрованный список назначенных матчей (см. MatchesController).
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

    /// <summary>
    /// Доступ к заявкам дивизиона: организатор — по владению лигой, судья — если у него есть
    /// назначенный матч в этом дивизионе.
    /// </summary>
    public async Task EnsureDivisionRosterAsync(Guid divisionId, CancellationToken cancellationToken = default)
    {
        if (currentUser.Role is UserRole.Organizer or UserRole.Admin)
        {
            await ownershipGuard.EnsureDivisionAsync(divisionId, cancellationToken);
            return;
        }

        var assignedMatches = await accountService.GetAssignedMatchIdsAsync(
            currentUser.UserId,
            currentUser.TenantId,
            cancellationToken);

        if (assignedMatches.Count == 0)
            throw new ForbiddenException("У вас нет назначенных матчей.");

        var matches = await matchService.GetByIdsAsync(assignedMatches, cancellationToken);

        if (matches.All(match => match.DivisionId != divisionId))
            throw new ForbiddenException("Этот дивизион вам не назначен.");
    }
}
