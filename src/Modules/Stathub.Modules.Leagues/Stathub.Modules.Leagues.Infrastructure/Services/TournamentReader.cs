using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Leagues.Domain.Enums;

namespace Stathub.Modules.Leagues.Infrastructure.Services;

internal sealed class TournamentReader(
    ITournamentRepository repository,
    ILeagueRepository leagueRepository) : ITournamentReader
{
    public async Task<TournamentStatusInfoDto?> GetAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        var tournament = await repository.GetByIdAsync(tournamentId, cancellationToken);

        if (tournament is null)
            return null;

        // Завершённый или отменённый турнир закрыт для правок состава.
        var rosterLocked = tournament.Status is TournamentStatus.Completed or TournamentStatus.Cancelled;

        return new TournamentStatusInfoDto(tournament.Id, tournament.LeagueId, rosterLocked);
    }

    public async Task<IReadOnlyList<Guid>> GetOrganizerIdsAsync(
        IReadOnlyCollection<Guid> tournamentIds,
        CancellationToken cancellationToken = default)
    {
        // Турниров у одной команды/игрока единицы, поэтому отдельный пакетный запрос не нужен.
        var leagueIds = new HashSet<Guid>();
        foreach (var tournamentId in tournamentIds.Distinct())
        {
            var tournament = await repository.GetByIdAsync(tournamentId, cancellationToken);
            if (tournament is not null)
                leagueIds.Add(tournament.LeagueId);
        }

        var organizerIds = new HashSet<Guid>();
        foreach (var leagueId in leagueIds)
        {
            var league = await leagueRepository.GetByIdAsync(leagueId, cancellationToken);
            if (league is not null)
                organizerIds.Add(league.OrganizerId);
        }

        return organizerIds.ToList();
    }

    public async Task<TournamentPublicInfoDto?> GetPublicInfoAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        var tournament = await repository.GetByIdAsync(tournamentId, cancellationToken);

        if (tournament is null)
            return null;

        var league = await leagueRepository.GetByIdAsync(tournament.LeagueId, cancellationToken);

        if (league is null)
            return null;

        return new TournamentPublicInfoDto(
            tournament.Id,
            tournament.Name,
            league.Id,
            league.Name,
            league.Slug,
            league.LogoUrl,
            league.Status == LeagueStatus.Published);
    }
}
