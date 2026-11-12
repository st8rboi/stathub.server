using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Modules.Leagues.Domain.ValueObjects;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Domain.Entities;

/// <summary>
/// Этап дивизиона (круг, плей-офф): формат, правила подсчёта очков, регламент матча,
/// а у кругового этапа — ещё группы и число команд, выходящих дальше.
/// </summary>
public sealed class Stage : Entity
{
    private readonly List<StageTiebreakerRule> _tiebreakerRules = [];
    private readonly List<StageGroup> _groups = [];

    public Guid TournamentId { get; private set; }
    public Guid DivisionId { get; private set; }

    /// <summary>Порядок этапа внутри дивизиона.</summary>
    public int Order { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public StageFormatType FormatType { get; private set; }
    public PointsRule PointsRule { get; private set; } = null!;
    public MatchFormatRule MatchFormatRule { get; private set; } = null!;

    /// <summary>
    /// Сколько первых команд таблицы выходит в следующий этап; при группах — из каждой группы.
    /// <c>null</c> — этап никого дальше не выводит (обычный чемпионат).
    /// </summary>
    public int? QualifiersCount { get; private set; }

    public IReadOnlyCollection<StageTiebreakerRule> TiebreakerRules => _tiebreakerRules.AsReadOnly();
    public IReadOnlyCollection<StageGroup> Groups => _groups.AsReadOnly();

    private Stage()
    {
    }

    private Stage(
        Guid id,
        Guid tournamentId,
        Guid divisionId,
        int order,
        string name,
        StageFormatType formatType,
        PointsRule pointsRule,
        MatchFormatRule matchFormatRule,
        int? qualifiersCount)
    {
        Id = id;
        TournamentId = tournamentId;
        DivisionId = divisionId;
        Order = order;
        Name = name;
        FormatType = formatType;
        PointsRule = pointsRule;
        MatchFormatRule = matchFormatRule;
        QualifiersCount = qualifiersCount;
    }

    internal static Stage Create(
        Guid tournamentId,
        Guid divisionId,
        int order,
        string name,
        StageFormatType formatType,
        PointsRule pointsRule,
        MatchFormatRule matchFormatRule,
        int? qualifiersCount = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Stage name обязательное поле.");

        if (!Enum.IsDefined(formatType))
            throw new DomainException($"Неизвестный формат этапа '{formatType}'.");

        ArgumentNullException.ThrowIfNull(pointsRule);
        ArgumentNullException.ThrowIfNull(matchFormatRule);
        EnsureQualifiers(formatType, qualifiersCount);

        return new Stage(
            Guid.NewGuid(),
            tournamentId,
            divisionId,
            order,
            name.Trim(),
            formatType,
            pointsRule,
            matchFormatRule,
            qualifiersCount);
    }

    internal bool HasDifferentRules(
        StageFormatType formatType,
        PointsRule pointsRule,
        MatchFormatRule matchFormatRule) =>
        FormatType != formatType || PointsRule != pointsRule || MatchFormatRule != matchFormatRule;

    /// <summary>Правка этапа; когда её можно делать, решает агрегат <see cref="Tournament"/>.</summary>
    internal void Update(
        string name,
        StageFormatType formatType,
        PointsRule pointsRule,
        MatchFormatRule matchFormatRule,
        int? qualifiersCount)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Название этапа обязательное поле.");

        if (!Enum.IsDefined(formatType))
            throw new DomainException($"Неизвестный формат этапа '{formatType}'.");

        ArgumentNullException.ThrowIfNull(pointsRule);
        ArgumentNullException.ThrowIfNull(matchFormatRule);
        EnsureQualifiers(formatType, qualifiersCount);

        if (formatType != StageFormatType.Round && _groups.Count > 0)
            throw new DomainException("У этапа есть группы: сначала удалите их, затем меняйте формат на плей-офф.");

        Name = name.Trim();
        FormatType = formatType;
        QualifiersCount = qualifiersCount;

        // Value object заменяем, только если он действительно изменился: EF отслеживает owned-типы
        // по ссылке, и без нужды подменять их не стоит.
        if (PointsRule != pointsRule)
            PointsRule = pointsRule;

        if (MatchFormatRule != matchFormatRule)
            MatchFormatRule = matchFormatRule;
    }

    internal void SetOrder(int order) => Order = order;

    public StageTiebreakerRule AddTiebreakerRule(int priority, TiebreakerCriterion criterion)
    {
        if (_tiebreakerRules.Any(r => r.Priority == priority))
            throw new ConflictException($"Критерий приоритета {priority} уже существует для этого этапа.");

        var rule = StageTiebreakerRule.Create(Id, priority, criterion);
        _tiebreakerRules.Add(rule);
        return rule;
    }

    /// <summary>
    /// Новая группа. Первую группу нельзя добавить, когда матчи уже есть: они созданы без группы
    /// и не попали бы ни в одну таблицу.
    /// </summary>
    internal StageGroup AddGroup(string name, IReadOnlyCollection<Guid> teamIds, bool stageHasMatches)
    {
        if (FormatType != StageFormatType.Round)
            throw new DomainException("Группы бывают только у кругового этапа.");

        if (stageHasMatches && _groups.Count == 0)
            throw new ConflictException(
                "В этапе уже есть матчи без групп: разбить его на группы можно только до создания матчей.");

        EnsureTeamsAreFree(teamIds, exceptGroupId: null);

        var group = StageGroup.Create(Id, _groups.Count + 1, name, teamIds);
        _groups.Add(group);
        return group;
    }

    /// <summary>
    /// Правка группы. Когда матчи уже есть, команды можно только добавлять: матч помнит свою
    /// группу, и убранная команда оставила бы в таблице «чужие» результаты.
    /// </summary>
    internal StageGroup UpdateGroup(Guid groupId, string name, IReadOnlyCollection<Guid> teamIds, bool stageHasMatches)
    {
        var group = GetGroup(groupId);

        if (stageHasMatches && group.TeamIds.Except(teamIds).Any())
            throw new ConflictException(
                "В этапе уже есть матчи: убирать команды из группы нельзя, можно только добавлять.");

        EnsureTeamsAreFree(teamIds, exceptGroupId: groupId);

        group.Update(name, teamIds);
        return group;
    }

    internal void RemoveGroup(Guid groupId, bool stageHasMatches)
    {
        var group = GetGroup(groupId);

        if (stageHasMatches)
            throw new ConflictException("В этапе уже есть матчи: удалить группу нельзя.");

        _groups.Remove(group);

        var order = 1;
        foreach (var remaining in _groups.OrderBy(item => item.Order))
            remaining.SetOrder(order++);
    }

    private StageGroup GetGroup(Guid groupId) =>
        _groups.FirstOrDefault(group => group.Id == groupId)
        ?? throw new NotFoundException($"Группа '{groupId}' не найдена в этапе '{Id}'.");

    private void EnsureTeamsAreFree(IReadOnlyCollection<Guid> teamIds, Guid? exceptGroupId)
    {
        ArgumentNullException.ThrowIfNull(teamIds);

        var taken = _groups
            .Where(group => group.Id != exceptGroupId)
            .SelectMany(group => group.TeamIds)
            .ToHashSet();

        if (teamIds.Any(taken.Contains))
            throw new ConflictException("Команда уже состоит в другой группе этого этапа.");
    }

    private static void EnsureQualifiers(StageFormatType formatType, int? qualifiersCount)
    {
        if (qualifiersCount is null)
            return;

        if (formatType != StageFormatType.Round)
            throw new DomainException("Число выходящих дальше команд задаётся только у кругового этапа.");

        if (qualifiersCount <= 0)
            throw new DomainException("Число выходящих дальше команд должно быть положительным.");
    }
}
