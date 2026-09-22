using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Modules.Leagues.Domain.ValueObjects;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Domain.Entities;

/// <summary>
/// Представляет этап турнира, содержащий информацию о его формате, правилах подсчета очков и формате матчей.
/// </summary>
public sealed class Stage : Entity
{
    private readonly List<StageTiebreakerRule> _tiebreakerRules = [];

    public Guid TournamentId { get; private set; }
    public int Order { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public StageFormatType FormatType { get; private set; }
    public PointsRule PointsRule { get; private set; } = null!;
    public MatchFormatRule MatchFormatRule { get; private set; } = null!;

    public IReadOnlyCollection<StageTiebreakerRule> TiebreakerRules => _tiebreakerRules.AsReadOnly();

    private Stage()
    {
    }

    private Stage(
        Guid id,
        Guid tournamentId,
        int order,
        string name,
        StageFormatType formatType,
        PointsRule pointsRule,
        MatchFormatRule matchFormatRule)
    {
        Id = id;
        TournamentId = tournamentId;
        Order = order;
        Name = name;
        FormatType = formatType;
        PointsRule = pointsRule;
        MatchFormatRule = matchFormatRule;
    }

    internal static Stage Create(
        Guid tournamentId,
        int order,
        string name,
        StageFormatType formatType,
        PointsRule pointsRule,
        MatchFormatRule matchFormatRule)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Stage name обязательное поле.");

        if (!Enum.IsDefined(formatType))
            throw new DomainException($"Неизвестный формат этапа '{formatType}'.");

        ArgumentNullException.ThrowIfNull(pointsRule);
        ArgumentNullException.ThrowIfNull(matchFormatRule);

        return new Stage(Guid.NewGuid(), tournamentId, order, name.Trim(), formatType, pointsRule, matchFormatRule);
    }

    public StageTiebreakerRule AddTiebreakerRule(int priority, TiebreakerCriterion criterion)
    {
        if (_tiebreakerRules.Any(r => r.Priority == priority))
            throw new ConflictException($"Критерий приоритета {priority} уже существует для этого этапа.");

        var rule = StageTiebreakerRule.Create(Id, priority, criterion);
        _tiebreakerRules.Add(rule);
        return rule;
    }
}
