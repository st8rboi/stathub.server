using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Domain.Entities;

public sealed class StageTiebreakerRule : Entity
{
    public Guid StageId { get; private set; }
    public int Priority { get; private set; }
    public TiebreakerCriterion Criterion { get; private set; }

    private StageTiebreakerRule()
    {
    }

    private StageTiebreakerRule(Guid id, Guid stageId, int priority, TiebreakerCriterion criterion)
    {
        Id = id;
        StageId = stageId;
        Priority = priority;
        Criterion = criterion;
    }

    internal static StageTiebreakerRule Create(Guid stageId, int priority, TiebreakerCriterion criterion)
    {
        if (priority < 0)
            throw new DomainException("Priority cannot be negative.");

        return new StageTiebreakerRule(Guid.NewGuid(), stageId, priority, criterion);
    }
}
