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
    /// <summary>
    /// Своя таблица истории миграций, как у остальных модулей. До её появления миграции Discipline
    /// писались в общую "__EFMigrationsHistory" — перенос записей см. в DatabaseMigrationInitializer.
    /// </summary>
    public const string HistoryTable = "__EFMigrationsHistory_Discipline";

    /// <summary>Таблица истории по умолчанию, куда писались миграции Discipline раньше.</summary>
    public const string LegacyHistoryTable = "__EFMigrationsHistory";

    public static IServiceCollection AddDisciplineModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Строка подключения 'Postgres' не настроена");

        services.AddDbContext<DisciplineDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(
                    HistoryTable,
                    "public")));

        services.AddScoped<IDisqualificationRepository, DisqualificationRepository>();
        services.AddScoped<ITeamMatchesSource, TeamMatchesSource>();
        services.AddScoped<IDisciplineDirectory, DisciplineDirectory>();
        services.AddScoped<DisciplineService>();

        return services;
    }

    /// <summary>
    /// Переносит записи о применённых миграциях Discipline из общей таблицы истории в свою.
    /// Без этого на уже развёрнутой базе EF не найдёт их в новой таблице и попытается
    /// применить InitialDiscipline повторно (таблицы уже есть → падение при старте).
    /// Идемпотентно; на чистой базе (нет общей таблицы) ничего не делает.
    /// Вызывать до MigrateAsync. Имена таблиц в SQL совпадают с HistoryTable / LegacyHistoryTable.
    /// </summary>
    public static async Task MoveLegacyMigrationHistoryAsync(
        DisciplineDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var legacyExists = await dbContext.Database
            .SqlQuery<bool>($"SELECT to_regclass({"public.\"" + LegacyHistoryTable + "\""}) IS NOT NULL AS \"Value\"")
            .SingleAsync(cancellationToken);

        if (!legacyExists)
            return;

        var migrationIds = dbContext.Database.GetMigrations().ToArray();

        // Та же структура, что создаёт EF для таблицы истории.
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS public."__EFMigrationsHistory_Discipline" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory_Discipline" PRIMARY KEY ("MigrationId")
            )
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO public."__EFMigrationsHistory_Discipline" ("MigrationId", "ProductVersion")
            SELECT "MigrationId", "ProductVersion"
            FROM public."__EFMigrationsHistory"
            WHERE "MigrationId" = ANY({migrationIds})
            ON CONFLICT ("MigrationId") DO NOTHING
            """,
            cancellationToken);
    }
}
