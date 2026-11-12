using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Domain.Entities;

/// <summary>
/// Группа кругового этапа (группы A–D кубка): подмножество команд со своей таблицей.
/// В отличие от дивизиона, группа не самостоятельное соревнование — команды из разных групп
/// сходятся в следующем этапе, а статистика у них общая.
/// Команды хранятся идентификаторами: сами команды живут в модуле Teams.
/// </summary>
public sealed class StageGroup : Entity
{
    private readonly List<Guid> _teamIds = [];

    public Guid StageId { get; private set; }
    public int Order { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public IReadOnlyList<Guid> TeamIds => _teamIds.AsReadOnly();

    private StageGroup()
    {
    }

    private StageGroup(Guid id, Guid stageId, int order, string name)
    {
        Id = id;
        StageId = stageId;
        Order = order;
        Name = name;
    }

    internal static StageGroup Create(Guid stageId, int order, string name, IEnumerable<Guid> teamIds)
    {
        var group = new StageGroup(Guid.NewGuid(), stageId, order, NormalizeName(name));
        group.ReplaceTeams(teamIds);
        return group;
    }

    internal void Update(string name, IEnumerable<Guid> teamIds)
    {
        Name = NormalizeName(name);
        ReplaceTeams(teamIds);
    }

    internal void SetOrder(int order) => Order = order;

    private void ReplaceTeams(IEnumerable<Guid> teamIds)
    {
        ArgumentNullException.ThrowIfNull(teamIds);

        var distinct = teamIds.Distinct().ToList();
        if (distinct.Contains(Guid.Empty))
            throw new DomainException("Команда группы указана некорректно.");

        // Список не трогаем без изменений: EF сравнивает содержимое, но лишняя замена ни к чему.
        if (distinct.SequenceEqual(_teamIds))
            return;

        _teamIds.Clear();
        _teamIds.AddRange(distinct);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Название группы обязательное поле.");

        return name.Trim();
    }
}
