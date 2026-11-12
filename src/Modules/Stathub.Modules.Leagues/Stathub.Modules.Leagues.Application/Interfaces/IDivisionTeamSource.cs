namespace Stathub.Modules.Leagues.Application.Interfaces;

/// <summary>
/// Команды, заявленные в дивизион. Объявлен здесь, а реализуется модулем заявок: Leagues
/// не знает про Rosters, но при составлении групп проверяет, что команда играет в этом дивизионе.
/// </summary>
public interface IDivisionTeamSource
{
    Task<IReadOnlyCollection<Guid>> GetTeamIdsAsync(Guid divisionId, CancellationToken cancellationToken = default);
}
