using Stathub.Modules.Rosters.Domain.Entities;

namespace Stathub.Modules.Rosters.Application.Interfaces;

public interface ITournamentTeamRepository
{
    /// <summary>Заявка с составом (отслеживается EF: её меняют операции заявок).</summary>
    Task<TournamentTeam?> GetByIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);

    Task<TournamentTeam?> GetByDivisionAndTeamAsync(
        Guid divisionId,
        Guid teamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Заявка команды в турнире независимо от дивизиона: команда играет только в одном дивизионе турнира.
    /// </summary>
    Task<TournamentTeam?> GetByTournamentAndTeamAsync(
        Guid tournamentId,
        Guid teamId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TournamentTeam>> ListByDivisionAsync(
        Guid divisionId,
        CancellationToken cancellationToken = default);

    /// <summary>Все заявки команды (во всех дивизионах и турнирах) вместе с составами — только чтение.</summary>
    Task<IReadOnlyList<TournamentTeam>> ListByTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Заявка команды, за которую игрок сейчас заявлен в турнире. Нужна, чтобы не допустить игрока
    /// в две команды одного турнира (в том числе в разных дивизионах): переход оформляется трансфером.
    /// </summary>
    Task<Guid?> FindTeamIdWithActiveRegistrationAsync(
        Guid tournamentId,
        Guid playerId,
        CancellationToken cancellationToken = default);

    /// <summary>Кто из перечисленных игроков уже заявлен (активно) за какую-либо команду турнира.</summary>
    Task<IReadOnlyCollection<Guid>> FindPlayersWithActiveRegistrationAsync(
        Guid tournamentId,
        IReadOnlyCollection<Guid> playerIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(TournamentTeam tournamentTeam, CancellationToken cancellationToken = default);

    /// <summary>Удаление заявки команды вместе с заявками игроков (каскад внутри модуля).</summary>
    void Remove(TournamentTeam tournamentTeam);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
