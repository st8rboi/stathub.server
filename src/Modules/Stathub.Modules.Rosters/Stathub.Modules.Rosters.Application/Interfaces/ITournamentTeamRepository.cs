using Stathub.Modules.Rosters.Domain.Entities;

namespace Stathub.Modules.Rosters.Application.Interfaces;

public interface ITournamentTeamRepository
{
    /// <summary>Заявка с составом (отслеживается EF: её меняют операции заявок).</summary>
    Task<TournamentTeam?> GetByIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);

    Task<TournamentTeam?> GetByTournamentAndTeamAsync(
        Guid tournamentId,
        Guid teamId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TournamentTeam>> ListByTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Команда турнира, за которую игрок заявлен сейчас. Нужна, чтобы не допустить игрока
    /// в две команды одного турнира: переход оформляется трансфером.
    /// </summary>
    Task<Guid?> FindTeamIdWithActiveRegistrationAsync(
        Guid tournamentId,
        Guid playerId,
        CancellationToken cancellationToken = default);

    Task AddAsync(TournamentTeam tournamentTeam, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
