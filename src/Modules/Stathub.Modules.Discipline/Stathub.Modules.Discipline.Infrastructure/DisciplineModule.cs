using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stathub.Modules.Discipline.Application;
using Stathub.Modules.Discipline.Domain.Entities;
using Stathub.Modules.Matches.Application;
using Stathub.Modules.Matches.Application.Dtos;
using Stathub.Modules.Matches.Domain.Enums;
using Stathub.Modules.Players.Application.Interfaces;
using Stathub.Modules.Teams.Application.Interfaces;

namespace Stathub.Modules.Discipline.Infrastructure.Persistence;

public sealed class DisciplineDbContext(DbContextOptions<DisciplineDbContext> options) : DbContext(options)
{
    public DbSet<Disqualification> Disqualifications => Set<Disqualification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("discipline");

        var builder = modelBuilder.Entity<Disqualification>();
        builder.ToTable("disqualifications");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.TournamentId).IsRequired();
        builder.Property(item => item.TeamId).IsRequired();
        builder.Property(item => item.PlayerId).IsRequired();
        builder.Property(item => item.Reason).IsRequired().HasMaxLength(500);
        builder.Property(item => item.MatchesBanned);
        builder.Property(item => item.IssuedAtUtc).IsRequired();
        builder.Property(item => item.IssuedByUserId);
        builder.Property(item => item.LiftedAtUtc);
        builder.Property(item => item.Comment).HasMaxLength(500);

        builder.HasIndex(item => new { item.TournamentId, item.TeamId, item.PlayerId });
    }
}

internal sealed class DisqualificationRepository(DisciplineDbContext dbContext) : IDisqualificationRepository
{
    public Task<Disqualification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Disqualifications.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Disqualification>> ListByTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Disqualifications
            .AsNoTracking()
            .Where(item => item.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

    public Task<Disqualification?> FindActiveAsync(
        Guid tournamentId,
        Guid teamId,
        Guid playerId,
        CancellationToken cancellationToken = default) =>
        dbContext.Disqualifications.FirstOrDefaultAsync(
            item => item.TournamentId == tournamentId
                    && item.TeamId == teamId
                    && item.PlayerId == playerId
                    && item.LiftedAtUtc == null,
            cancellationToken);

    public async Task AddAsync(Disqualification disqualification, CancellationToken cancellationToken = default) =>
        await dbContext.Disqualifications.AddAsync(disqualification, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Завершённые матчи команды после выдачи дисквалификации. Берём матчи интервалом
/// от даты выдачи и фильтруем по команде и статусу — отдельный читатель не нужен.
/// </summary>
internal sealed class TeamMatchesSource(MatchService matchService) : ITeamMatchesSource
{
    public async Task<int> CountFinishedSinceAsync(
        Guid tournamentId,
        Guid teamId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        var from = sinceUtc.AddDays(-1);
        var to = DateTime.UtcNow.AddYears(1);
        var matches = await matchService.ListByPeriodAsync(from, to, cancellationToken);

        return matches.Count(match =>
            match.TournamentId == tournamentId
            && match.Status == MatchStatus.Finished
            && (match.HomeTeamId == teamId || match.AwayTeamId == teamId));
    }
}

internal sealed class DisciplineDirectory(
    ITeamReader teamReader,
    IPlayerReader playerReader) : IDisciplineDirectory
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetTeamNamesAsync(
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken = default)
    {
        if (teamIds.Count == 0)
            return new Dictionary<Guid, string>();

        var teams = await teamReader.GetNamesAsync(teamIds, cancellationToken);
        return teams.ToDictionary(team => team.Id, team => team.Name);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetPlayerNamesAsync(
        IReadOnlyCollection<Guid> playerIds,
        CancellationToken cancellationToken = default)
    {
        if (playerIds.Count == 0)
            return new Dictionary<Guid, string>();

        var players = await playerReader.GetNamesAsync(playerIds, cancellationToken);
        return players.ToDictionary(player => player.Id, player => player.FullName);
    }
}

public static class DisciplineModule
{
    public static IServiceCollection AddDisciplineModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Строка подключения 'Postgres' не настроена");

        services.AddDbContext<DisciplineDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IDisqualificationRepository, DisqualificationRepository>();
        services.AddScoped<ITeamMatchesSource, TeamMatchesSource>();
        services.AddScoped<IDisciplineDirectory, DisciplineDirectory>();
        services.AddScoped<DisciplineService>();

        return services;
    }
}
