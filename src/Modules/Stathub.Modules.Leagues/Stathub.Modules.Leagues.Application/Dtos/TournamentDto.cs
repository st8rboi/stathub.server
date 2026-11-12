using Stathub.Modules.Leagues.Domain.Entities;
using Stathub.Modules.Leagues.Domain.Enums;

namespace Stathub.Modules.Leagues.Application.Dtos;

public sealed record TournamentSummaryDto(
    Guid Id,
    Guid LeagueId,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    TournamentStatus Status)
{
    internal static TournamentSummaryDto From(Tournament tournament) => new(
        tournament.Id,
        tournament.LeagueId,
        tournament.Name,
        tournament.StartDate,
        tournament.EndDate,
        tournament.Status);
}

/// <summary>
/// Турнир со структурой. <see cref="Divisions"/> — дерево «дивизион → этапы»;
/// <see cref="Stages"/> — те же этапы плоским списком (порядок: дивизион, затем этап).
/// </summary>
public sealed record TournamentDto(
    Guid Id,
    Guid LeagueId,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    TournamentStatus Status,
    IReadOnlyList<StageDto> Stages,
    IReadOnlyList<DivisionDto> Divisions)
{
    internal static TournamentDto From(Tournament tournament)
    {
        var divisions = tournament.Divisions
            .OrderBy(division => division.Order)
            .Select(division => DivisionDto.From(division, tournament.Stages))
            .ToList();

        return new TournamentDto(
            tournament.Id,
            tournament.LeagueId,
            tournament.Name,
            tournament.StartDate,
            tournament.EndDate,
            tournament.Status,
            divisions.SelectMany(division => division.Stages).ToList(),
            divisions);
    }
}

public sealed record DivisionDto(
    Guid Id,
    int Order,
    string Name,
    IReadOnlyList<StageDto> Stages)
{
    internal static DivisionDto From(Division division, IEnumerable<Stage> tournamentStages) => new(
        division.Id,
        division.Order,
        division.Name,
        tournamentStages
            .Where(stage => stage.DivisionId == division.Id)
            .OrderBy(stage => stage.Order)
            .Select(StageDto.From)
            .ToList());
}

public sealed record StageGroupDto(
    Guid Id,
    int Order,
    string Name,
    IReadOnlyList<Guid> TeamIds)
{
    internal static StageGroupDto From(StageGroup group) => new(
        group.Id,
        group.Order,
        group.Name,
        group.TeamIds.ToList());
}

public sealed record StageDto(
    Guid Id,
    Guid DivisionId,
    int Order,
    string Name,
    StageFormatType FormatType,
    int WinPoints,
    int DrawPoints,
    int LossPoints,
    int PeriodDurationMinutes,
    int PeriodsCount,
    bool ExtraTimeEnabled,
    bool PenaltyShootoutEnabled,
    int? QualifiersCount,
    IReadOnlyList<StageGroupDto> Groups)
{
    internal static StageDto From(Stage stage) => new(
        stage.Id,
        stage.DivisionId,
        stage.Order,
        stage.Name,
        stage.FormatType,
        stage.PointsRule.WinPoints,
        stage.PointsRule.DrawPoints,
        stage.PointsRule.LossPoints,
        stage.MatchFormatRule.PeriodDurationMinutes,
        stage.MatchFormatRule.PeriodsCount,
        stage.MatchFormatRule.ExtraTimeEnabled,
        stage.MatchFormatRule.PenaltyShootoutEnabled,
        stage.QualifiersCount,
        stage.Groups.OrderBy(group => group.Order).Select(StageGroupDto.From).ToList());
}
