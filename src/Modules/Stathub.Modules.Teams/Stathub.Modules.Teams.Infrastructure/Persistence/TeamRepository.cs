using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Teams.Application.Interfaces;
using Stathub.Modules.Teams.Domain.Entities;

namespace Stathub.Modules.Teams.Infrastructure.Persistence;

internal sealed class TeamRepository(TeamsDbContext dbContext) : ITeamRepository
{
    public Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(team => team.Id == id, cancellationToken);

    public Task<Team?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Teams
            .FirstOrDefaultAsync(team => team.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Team>> ListAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Команды — небольшой справочник, пагинация здесь не нужна: limit защищает от случайного
        // запроса «все команды всех лиг» на будущих объёмах.
        var teams = dbContext.Teams.AsNoTracking();

        if (query is not null)
            teams = teams.Where(team => EF.Functions.ILike(team.Name, $"%{EscapeLike(query)}%"));

        return await teams
            .OrderBy(team => team.Name)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Team>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];

        return await dbContext.Teams
            .AsNoTracking()
            .Where(team => ids.Contains(team.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Team team, CancellationToken cancellationToken = default) =>
        await dbContext.Teams.AddAsync(team, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    // % и _ для LIKE значимы — экранируем пользовательский ввод.
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
