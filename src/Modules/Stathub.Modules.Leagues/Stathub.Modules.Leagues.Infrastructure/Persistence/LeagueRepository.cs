using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Leagues.Domain.Entities;
using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Infrastructure.Persistence;

internal sealed class LeagueRepository(LeaguesDbContext dbContext) : ILeagueRepository
{
    // Получить лигу по Id
    public Task<League?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Leagues.FirstOrDefaultAsync(l => l.Id == id, cancellationToken); 

    // Получить лигу по короткому имени
    public Task<League?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        dbContext.Leagues.FirstOrDefaultAsync(l => l.Slug == slug, cancellationToken);

    // Получить список лиг с фильтрацией по организатору и / или статусу
    public async Task<IReadOnlyList<League>> ListAsync(
        Guid? organizerId,
        LeagueStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Leagues.AsNoTracking();

        if (organizerId is not null)
            query = query.Where(l => l.OrganizerId == organizerId);

        if (status is not null)
            query = query.Where(l => l.Status == status);

        return await query.OrderByDescending(l => l.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    // Добавить лигу в бдшку
    public async Task AddAsync(League league, CancellationToken cancellationToken = default) =>
        await dbContext.Leagues.AddAsync(league, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Slug должен быть уникальным, даже при разных id
            throw new ConflictException("Лига с таким slug уже существует.");
        }
    }
}
