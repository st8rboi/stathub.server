using Stathub.Modules.Leagues.Application.Dtos;
using Stathub.Modules.Leagues.Application.Interfaces;
using Stathub.Modules.Leagues.Domain.Entities;

namespace Stathub.Modules.Leagues.Infrastructure.Services;

internal sealed class DivisionReader(ITournamentRepository tournamentRepository) : IDivisionReader
{
    public async Task<DivisionInfoDto?> GetAsync(Guid divisionId, CancellationToken cancellationToken = default)
    {
        var tournament = await tournamentRepository.GetByDivisionIdAsync(divisionId, cancellationToken);
        var division = tournament?.Divisions.FirstOrDefault(item => item.Id == divisionId);

        return tournament is null || division is null ? null : Map(tournament, division);
    }

    public async Task<IReadOnlyList<DivisionInfoDto>> ListByTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default)
    {
        var tournament = await tournamentRepository.GetByIdAsync(tournamentId, cancellationToken);
        if (tournament is null)
            return [];

        return tournament.Divisions
            .OrderBy(division => division.Order)
            .Select(division => Map(tournament, division))
            .ToList();
    }

    private static DivisionInfoDto Map(Tournament tournament, Division division) => new(
        division.Id,
        division.Name,
        division.Order,
        tournament.Id,
        tournament.Divisions.Count,
        tournament.Stages
            .Where(stage => stage.DivisionId == division.Id)
            .OrderBy(stage => stage.Order)
            .Select(stage => stage.Id)
            .ToList());
}
