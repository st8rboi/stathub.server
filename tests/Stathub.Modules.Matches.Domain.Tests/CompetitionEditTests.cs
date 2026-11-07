using Stathub.Modules.Leagues.Domain.Entities;
using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Modules.Leagues.Domain.ValueObjects;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;
using Xunit;

namespace Stathub.Modules.Matches.Domain.Tests;

/// <summary>Правка и удаление лиг, турниров и этапов.</summary>
public sealed class CompetitionEditTests
{
    private static readonly PointsRule Points = new(3, 1, 0);
    private static readonly MatchFormatRule Format = new(25, 2, false, true);

    private static League CreateLeague(string? city = "Казань") =>
        League.Create(Guid.NewGuid(), "Лига", "liga", Sport.Football, city);

    private static Tournament CreateTournamentWithStages(params string[] stageNames)
    {
        var tournament = Tournament.Create(Guid.NewGuid(), "Сезон", new DateOnly(2026, 4, 1));
        foreach (var name in stageNames)
            tournament.AddStage(name, StageFormatType.Round, Points, Format);

        return tournament;
    }

    [Fact]
    public void LeagueUpdate_NormalizesFields()
    {
        var league = CreateLeague();

        league.Update("  Новая лига ", " New-Liga ", " Казань ", "  ");

        Assert.Equal("Новая лига", league.Name);
        Assert.Equal("new-liga", league.Slug);
        Assert.Equal("Казань", league.City);
        Assert.Null(league.Region);
    }

    [Fact]
    public void LeagueUpdate_PublishedLeagueWithoutCity_Rejected()
    {
        var league = CreateLeague();
        league.Publish();

        Assert.Throws<DomainException>(() => league.Update("Лига", "liga", null, null));
    }

    [Fact]
    public void LeagueUpdate_DraftLeagueWithoutCity_Allowed()
    {
        var league = CreateLeague();

        league.Update("Лига", "liga", null, null);

        Assert.Null(league.City);
    }

    [Fact]
    public void LeagueDelete_WithTournaments_Conflict()
    {
        Assert.Throws<ConflictException>(() => League.EnsureCanDelete(1));
        League.EnsureCanDelete(0);
    }

    [Fact]
    public void TournamentUpdate_EndBeforeStart_Rejected()
    {
        var tournament = CreateTournamentWithStages();

        Assert.Throws<DomainException>(() =>
            tournament.Update("Сезон", new DateOnly(2026, 5, 1), new DateOnly(2026, 4, 1)));
    }

    [Fact]
    public void UpdateStage_ScheduledWithoutMatches_ChangesRules()
    {
        var tournament = CreateTournamentWithStages("Группа");
        var stageId = tournament.Stages.Single().Id;

        var stage = tournament.UpdateStage(
            stageId, "Группа А", StageFormatType.Round, new PointsRule(2, 1, 0), Format, stageHasMatches: false);

        Assert.Equal("Группа А", stage.Name);
        Assert.Equal(2, stage.PointsRule.WinPoints);
    }

    [Fact]
    public void UpdateStage_RulesWithMatches_Conflict()
    {
        var tournament = CreateTournamentWithStages("Группа");
        var stageId = tournament.Stages.Single().Id;

        Assert.Throws<ConflictException>(() => tournament.UpdateStage(
            stageId, "Группа", StageFormatType.Round, new PointsRule(2, 1, 0), Format, stageHasMatches: true));
    }

    [Fact]
    public void UpdateStage_RulesAfterStart_Conflict()
    {
        var tournament = CreateTournamentWithStages("Группа");
        var stageId = tournament.Stages.Single().Id;
        tournament.Start();

        Assert.Throws<ConflictException>(() => tournament.UpdateStage(
            stageId, "Группа", StageFormatType.Round, Points, new MatchFormatRule(30, 2, false, true), false));
    }

    [Fact]
    public void UpdateStage_RenameOnlyAfterStartWithMatches_Allowed()
    {
        var tournament = CreateTournamentWithStages("Группа");
        var stageId = tournament.Stages.Single().Id;
        tournament.Start();

        var stage = tournament.UpdateStage(
            stageId, "Группа А", StageFormatType.Round, new PointsRule(3, 1, 0), new MatchFormatRule(25, 2, false, true), true);

        Assert.Equal("Группа А", stage.Name);
    }

    [Fact]
    public void RemoveStage_RenumbersRemainingStages()
    {
        var tournament = CreateTournamentWithStages("Группа", "Полуфинал", "Финал");
        var semifinal = tournament.Stages.Single(s => s.Name == "Полуфинал");

        tournament.RemoveStage(semifinal.Id);

        Assert.Equal(
            [("Группа", 1), ("Финал", 2)],
            tournament.Stages.OrderBy(s => s.Order).Select(s => (s.Name, s.Order)).ToList());
    }

    [Fact]
    public void RemoveStage_UnknownStage_NotFound()
    {
        var tournament = CreateTournamentWithStages("Группа");

        Assert.Throws<NotFoundException>(() => tournament.RemoveStage(Guid.NewGuid()));
    }
}
