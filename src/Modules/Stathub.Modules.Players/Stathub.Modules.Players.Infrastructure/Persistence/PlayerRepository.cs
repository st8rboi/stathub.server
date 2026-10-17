using Microsoft.EntityFrameworkCore;
using Stathub.Modules.Players.Application.Interfaces;
using Stathub.Modules.Players.Domain.Entities;

namespace Stathub.Modules.Players.Infrastructure.Persistence;

internal sealed class PlayerRepository(PlayersDbContext dbContext) : IPlayerRepository
{
    /// <summary>
    /// Порог схожести для поиска подсказок.
    /// </summary>
    /// <remarks>
    /// Сравниваем <c>word_similarity</c> с порогом явно, а не через оператор <c>&lt;%</c>:
    /// поведение оператора зависит от сессионного параметра <c>word_similarity_threshold</c>,
    /// который задаётся в единицах 0..1 и меняется в зависимости от сборки Postgres.
    /// Явное сравнение делает порог видимым и предсказуемым.
    /// <para>
    /// 0.3 выведено из реальных замеров на ФИО:
    /// «Иванов Иван Иванович» против «иваан» = 0.667 (совпадение),
    /// «Сидоров Алексей» против «сидаров» = 0.455 (однобуквенная замена — совпадение),
    /// «Иванченко Мария» против «иваан» = 0.500, но это уже другой человек.
    /// </para>
    /// </remarks>
    private const double SimilarityThreshold = 0.3;

    public Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Player>> FindByNameAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // ПОРЯДОК АРГУМЕНТОВ принципиален, проверено на реальных данных:
        //   word_similarity('сидаров', 'Сидоров Алексей') = 0.455 -> находит
        //   word_similarity('Сидоров Алексей', 'сидаров') = 0.263 -> не находит
        // word-level берётся вместо сравнения со всей строкой (%), потому что последнее
        // «размывается» длиной ФИО: на той же опечатке даёт 0.263 против порога 0.3.
        //
        // ILIKE добавлен для частичных совпадений: при вводе 2-3 символов схожесть
        // ещё слишком низкая, и триграммы сами по себе не нашли бы ничего.
        var candidates = dbContext.Players
            .AsNoTracking()
            .Where(p => EF.Functions.TrigramsWordSimilarity(query, p.FullName) >= SimilarityThreshold
                        || EF.Functions.ILike(p.FullName, $"%{EscapeLike(query)}%"));

        return await candidates
            .OrderByDescending(p => EF.Functions.TrigramsWordSimilarity(query, p.FullName))
            .ThenByDescending(p => p.FullName.ToLower().StartsWith(query.ToLower()))
            .ThenBy(p => p.FullName)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Player>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken = default) =>
        await dbContext.Players
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAtUtc)
            .ThenBy(p => p.FullName)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Player>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];

        return await dbContext.Players
            .AsNoTracking()
            .Where(player => ids.Contains(player.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Player player, CancellationToken cancellationToken = default) =>
        await dbContext.Players.AddAsync(player, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    // Символы % и _ для LIKE значимы, поэтому в пользовательском вводе их нужно экранировать
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
