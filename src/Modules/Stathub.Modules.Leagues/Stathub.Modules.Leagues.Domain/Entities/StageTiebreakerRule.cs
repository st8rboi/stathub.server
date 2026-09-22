using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Domain.Entities;

/// <summary>
/// Правило для определения победителя в случае равенства очков между командами на этапе турнира.
/// </summary>
public sealed class StageTiebreakerRule : Entity
{
    public Guid StageId { get; private set; } // Идентификатор стадии, к которой относится правило
    public int Priority { get; private set; } // Приоритет правила (чем меньше число, тем выше приоритет)
    public TiebreakerCriterion Criterion { get; private set; } // Критерий, по которому определяется победитель (например, количество очков, разница голов и т.д.)

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

    /// <summary>
    /// Создает новое правило для определения победителя в случае равенства очков между командами на этапе турнира.
    /// </summary>
    /// <param name="stageId"></param>
    /// <param name="priority"></param>
    /// <param name="criterion"></param>
    /// <returns></returns>
    /// <exception cref="DomainException"></exception>
    internal static StageTiebreakerRule Create(Guid stageId, int priority, TiebreakerCriterion criterion)
    {
        if (priority < 0)
            throw new DomainException("Приоритет не может быть отрицательным.");

        return new StageTiebreakerRule(Guid.NewGuid(), stageId, priority, criterion);
    }
}
