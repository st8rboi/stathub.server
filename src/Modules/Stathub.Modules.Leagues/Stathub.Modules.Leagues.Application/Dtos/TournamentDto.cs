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

public sealed record TournamentDto(
    Guid Id,
    Guid LeagueId,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    TournamentStatus Status,
    IReadOnlyList<StageDto> Stages)
{
    internal static TournamentDto From(Tournament tournament) => new(
        tournament.Id,
        tournament.LeagueId,
        tournament.Name,
        tournament.StartDate,
        tournament.EndDate,
        tournament.Status,
        tournament.Stages.OrderBy(s => s.Order).Select(StageDto.From).ToList());
}

public sealed record StageDto(
    Guid Id,
    int Order,
    string Name,
    StageFormatType FormatType,
    int WinPoints,
    int DrawPoints,
    int LossPoints,
    int PeriodDurationMinutes,
    int PeriodsCount,
    bool ExtraTimeEnabled,
    bool PenaltyShootoutEnabled)
{
    internal static StageDto From(Stage stage) => new(
        stage.Id,
        stage.Order,
        stage.Name,
        stage.FormatType,
        stage.PointsRule.WinPoints,
        stage.PointsRule.DrawPoints,
        stage.PointsRule.LossPoints,
        stage.MatchFormatRule.PeriodDurationMinutes,
        stage.MatchFormatRule.PeriodsCount,
        stage.MatchFormatRule.ExtraTimeEnabled,
        stage.MatchFormatRule.PenaltyShootoutEnabled);
}
