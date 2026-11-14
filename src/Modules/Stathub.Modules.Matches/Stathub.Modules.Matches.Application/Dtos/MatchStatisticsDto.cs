using Stathub.Modules.Matches.Domain.Services;

namespace Stathub.Modules.Matches.Application.Dtos;

/// <summary>Командная статистика матча: проекция протокола (удары, фолы, угловые).</summary>
public sealed record MatchStatisticsDto(
    TeamMatchStatisticsDto Home,
    TeamMatchStatisticsDto Away)
{
    internal static MatchStatisticsDto From(MatchStatistics statistics) => new(
        TeamMatchStatisticsDto.From(statistics.Home),
        TeamMatchStatisticsDto.From(statistics.Away));
}

/// <summary><see cref="Shots"/> — все удары, включая удары в створ.</summary>
public sealed record TeamMatchStatisticsDto(int Shots, int ShotsOnTarget, int Fouls, int Corners)
{
    internal static TeamMatchStatisticsDto From(TeamMatchStatistics statistics) => new(
        statistics.Shots,
        statistics.ShotsOnTarget,
        statistics.Fouls,
        statistics.Corners);
}
