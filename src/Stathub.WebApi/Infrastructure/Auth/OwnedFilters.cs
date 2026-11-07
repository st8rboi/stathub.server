using Microsoft.AspNetCore.Mvc.Filters;

namespace Stathub.WebApi.Infrastructure.Auth;

/// <summary>
/// Фильтр для маршрутов с <c>{leagueId}</c>: до выполнения действия проверяет,
/// что лига принадлежит организации из токена. Ставится на контроллер целиком,
/// чтобы новое действие нельзя было случайно оставить без проверки.
/// </summary>
public sealed class OwnedLeagueFilter(TenantOwnershipGuard ownershipGuard) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.RouteData.Values.TryGetValue("leagueId", out var raw) &&
            Guid.TryParse(raw?.ToString(), out var leagueId))
        {
            await ownershipGuard.EnsureLeagueAsync(leagueId, context.HttpContext.RequestAborted);
        }

        await next();
    }
}

/// <summary>То же для маршрутов с <c>{tournamentId}</c>: проверка идёт через лигу турнира.</summary>
public sealed class OwnedTournamentFilter(TenantOwnershipGuard ownershipGuard) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.RouteData.Values.TryGetValue("tournamentId", out var raw) &&
            Guid.TryParse(raw?.ToString(), out var tournamentId))
        {
            await ownershipGuard.EnsureTournamentAsync(tournamentId, context.HttpContext.RequestAborted);
        }

        await next();
    }
}

/// <summary>То же для маршрутов с <c>{stageId}</c>: этап → турнир → лига.</summary>
public sealed class OwnedStageFilter(TenantOwnershipGuard ownershipGuard) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.RouteData.Values.TryGetValue("stageId", out var raw) &&
            Guid.TryParse(raw?.ToString(), out var stageId))
        {
            await ownershipGuard.EnsureStageAsync(stageId, context.HttpContext.RequestAborted);
        }

        await next();
    }
}

/// <summary>
/// Заявки этапа: организатор — по владению, судья — если у него есть матч на этом этапе.
/// </summary>
public sealed class StageRosterAccessFilter(MatchAccessGuard matchAccessGuard) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.RouteData.Values.TryGetValue("stageId", out var raw) &&
            Guid.TryParse(raw?.ToString(), out var stageId))
        {
            await matchAccessGuard.EnsureStageRosterAsync(stageId, context.HttpContext.RequestAborted);
        }

        await next();
    }
}
