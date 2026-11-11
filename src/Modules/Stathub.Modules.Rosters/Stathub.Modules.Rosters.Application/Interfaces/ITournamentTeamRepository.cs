using Stathub.Modules.Rosters.Domain.Entities;

namespace Stathub.Modules.Rosters.Application.Interfaces;

public interface ITournamentTeamRepository
{
    /// <summary>Заявка с составом (отслеживается EF: её меняют операции заявок).</summary>
    Task<TournamentTeam?> GetByIdAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);

    Task<TournamentTeam?> GetByStageAndTeamAsync(
        Guid stageId,
        Guid teamId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TournamentTeam>> ListByStageAsync(
        Guid stageId,
        CancellationToken cancellationToken = default);

    /// <summary>Все заявки команды (во всех этапах и турнирах) вместе с составами — только чтение.</summary>
    Task<IReadOnlyList<TournamentTeam>> ListByTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Команда этапа, за которую игрок заявлен сейчас. Нужна, чтобы не допустить игрока
    /// в две команды одного дивизиона: переход оформляется трансфером. В разных дивизионах
    /// того же турнира игрок играть может.
    /// </summary>
    Task<Guid?> FindTeamIdWithActiveRegistrationAsync(
        Guid stageId,
        Guid playerId,
        CancellationToken cancellationToken = default);

    Task AddAsync(TournamentTeam tournamentTeam, CancellationToken cancellationToken = default);

    /// <summary>Удаление заявки команды вместе с заявками игроков (каскад внутри модуля).</summary>
    void Remove(TournamentTeam tournamentTeam);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
