using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Matches.Domain.Services;

/// <summary>
/// Определяет группу матча по командам. Группу не выбирают вручную: она следует из того,
/// в какой группе этапа состоят обе команды, поэтому матч не может попасть «не в ту» таблицу.
/// </summary>
public static class MatchGroupResolver
{
    /// <param name="groups">Группы этапа с их командами; пусто — этап без групп.</param>
    /// <returns>Группа матча или <c>null</c>, если этап на группы не разбит.</returns>
    public static Guid? Resolve(
        IReadOnlyCollection<(Guid GroupId, IReadOnlyCollection<Guid> TeamIds)> groups,
        Guid homeTeamId,
        Guid awayTeamId)
    {
        ArgumentNullException.ThrowIfNull(groups);

        if (groups.Count == 0)
            return null;

        var homeGroup = FindGroup(groups, homeTeamId)
            ?? throw new DomainException("Хозяева не входят ни в одну группу этого этапа.");
        var awayGroup = FindGroup(groups, awayTeamId)
            ?? throw new DomainException("Гости не входят ни в одну группу этого этапа.");

        if (homeGroup != awayGroup)
            throw new DomainException("Команды состоят в разных группах: на групповом этапе играют только внутри группы.");

        return homeGroup;
    }

    private static Guid? FindGroup(
        IReadOnlyCollection<(Guid GroupId, IReadOnlyCollection<Guid> TeamIds)> groups,
        Guid teamId)
    {
        foreach (var (groupId, teamIds) in groups)
        {
            if (teamIds.Contains(teamId))
                return groupId;
        }

        return null;
    }
}
