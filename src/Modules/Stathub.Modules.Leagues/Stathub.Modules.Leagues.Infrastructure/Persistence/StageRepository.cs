using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Leagues.Domain.Entities;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Infrastructure.Persistence;

internal sealed class StageRepository(LeaguesDbContext dbContext) : IStageRepository
{
    // Получить стадию по Id
    public Task<Stage?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        dbContext.Stages
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    // Добавить стадию в БД
    public async Task AddAsync(
        Stage stage,
        CancellationToken cancellationToken = default) =>
        await dbContext.Stages.AddAsync(stage, cancellationToken);

    // Сохранить изменения
    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            throw new ConflictException(
                "Стадия с такими уникальными данными уже существует.");
        }
    }
}