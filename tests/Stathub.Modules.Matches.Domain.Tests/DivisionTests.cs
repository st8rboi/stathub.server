using Stathub.Modules.Leagues.Domain.Entities;
using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Modules.Leagues.Domain.ValueObjects;
using Stathub.Modules.Matches.Domain.Services;
using Stathub.Modules.Standings.Domain;
using Stathub.Modules.Standings.Domain.Enums;
using Stathub.Modules.Standings.Domain.Models;
using Stathub.Shared.Exceptions;
using Xunit;

namespace Stathub.Modules.Matches.Domain.Tests;

/// <summary>
/// Структура турнира «дивизион → этап → группа»: дивизион по умолчанию, порядок этапов внутри
/// дивизиона, группы кругового этапа, группа матча и зона выхода в следующий этап.
/// </summary>
public sealed class DivisionTests
{
    private static readonly PointsRule Points = new(3, 1, 0);
    private static readonly MatchFormatRule Format = new(25, 2, false, true);

    private static readonly Guid Alpha = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Beta = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Gamma = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Delta = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static Tournament CreateTournament() =>
        Tournament.Create(Guid.NewGuid(), "Сезон", new DateOnly(2026, 4, 1));

    private static Stage AddRound(Tournament tournament, Guid divisionId, string name = "Круг", int? qualifiers = null) =>
        tournament.AddStage(divisionId, name, StageFormatType.Round, Points, Format, qualifiers);

    [Fact]
    public void Create_AddsDefaultDivision()
    {
        var tournament = CreateTournament();

        var division = Assert.Single(tournament.Divisions);
        Assert.Equal(Tournament.DefaultDivisionName, division.Name);
        Assert.Equal(1, division.Order);
    }

    [Fact]
    public void AddStage_OrderIsCountedInsideDivision()
    {
        var tournament = CreateTournament();
        var first = tournament.Divisions.Single();
        var second = tournament.AddDivision("Второй дивизион");

        AddRound(tournament, first.Id);
        var playoff = tournament.AddStage(first.Id, "Плей-офф", StageFormatType.Playoff, Points, Format);
        var otherRound = AddRound(tournament, second.Id);

        Assert.Equal(2, playoff.Order);
        Assert.Equal(1, otherRound.Order);
        Assert.Equal(second.Id, otherRound.DivisionId);
    }

    [Fact]
    public void AddStage_UnknownDivision_NotFound()
    {
        var tournament = CreateTournament();

        Assert.Throws<NotFoundException>(() => AddRound(tournament, Guid.NewGuid()));
    }

    [Fact]
    public void RemoveStage_RenumbersOnlyItsDivision()
    {
        var tournament = CreateTournament();
        var first = tournament.Divisions.Single();
        var second = tournament.AddDivision("Второй дивизион");

        var round = AddRound(tournament, first.Id);
        var playoff = tournament.AddStage(first.Id, "Плей-офф", StageFormatType.Playoff, Points, Format);
        AddRound(tournament, second.Id, "Круг 2");
        var otherPlayoff = tournament.AddStage(second.Id, "Плей-офф 2", StageFormatType.Playoff, Points, Format);

        tournament.RemoveStage(round.Id);

        Assert.Equal(1, playoff.Order);
        Assert.Equal(2, otherPlayoff.Order);
    }

    [Fact]
    public void RemoveDivision_RemovesItsStagesAndRenumbers()
    {
        var tournament = CreateTournament();
        var first = tournament.Divisions.Single();
        var second = tournament.AddDivision("Второй дивизион");
        AddRound(tournament, first.Id);
        var kept = AddRound(tournament, second.Id);

        tournament.RemoveDivision(first.Id);

        Assert.Equal(1, Assert.Single(tournament.Divisions).Order);
        Assert.Equal(kept.Id, Assert.Single(tournament.Stages).Id);
    }

    [Fact]
    public void RemoveDivision_LastOne_Conflict()
    {
        var tournament = CreateTournament();

        Assert.Throws<ConflictException>(() => tournament.RemoveDivision(tournament.Divisions.Single().Id));
    }

    [Fact]
    public void AddDivision_AfterStart_Conflict()
    {
        var tournament = CreateTournament();
        tournament.Start();

        Assert.Throws<ConflictException>(() => tournament.AddDivision("Второй дивизион"));
    }

    [Fact]
    public void Qualifiers_OnlyForRoundStage_AndPositive()
    {
        var tournament = CreateTournament();
        var divisionId = tournament.Divisions.Single().Id;

        Assert.Equal(4, AddRound(tournament, divisionId, qualifiers: 4).QualifiersCount);
        Assert.Throws<DomainException>(() => AddRound(tournament, divisionId, "Круг 2", qualifiers: 0));
        Assert.Throws<DomainException>(() =>
            tournament.AddStage(divisionId, "Плей-офф", StageFormatType.Playoff, Points, Format, qualifiersCount: 2));
    }

    [Fact]
    public void UpdateStage_QualifiersCanChangeEvenWithMatches()
    {
        var tournament = CreateTournament();
        var stage = AddRound(tournament, tournament.Divisions.Single().Id);
        tournament.Start();

        tournament.UpdateStage(stage.Id, stage.Name, StageFormatType.Round, Points, Format, 2, stageHasMatches: true);

        Assert.Equal(2, stage.QualifiersCount);
    }

    [Fact]
    public void Groups_TeamBelongsToOnlyOneGroup()
    {
        var tournament = CreateTournament();
        var stage = AddRound(tournament, tournament.Divisions.Single().Id);

        var groupA = tournament.AddStageGroup(stage.Id, "Группа A", [Alpha, Beta], stageHasMatches: false);
        var groupB = tournament.AddStageGroup(stage.Id, "Группа B", [Gamma], stageHasMatches: false);

        Assert.Equal((1, 2), (groupA.Order, groupB.Order));
        Assert.Throws<ConflictException>(() =>
            tournament.AddStageGroup(stage.Id, "Группа C", [Alpha], stageHasMatches: false));
        Assert.Throws<ConflictException>(() =>
            tournament.UpdateStageGroup(stage.Id, groupB.Id, "Группа B", [Gamma, Beta], stageHasMatches: false));
    }

    [Fact]
    public void Groups_NotForPlayoff_AndBlockSwitchToPlayoff()
    {
        var tournament = CreateTournament();
        var divisionId = tournament.Divisions.Single().Id;
        var playoff = tournament.AddStage(divisionId, "Плей-офф", StageFormatType.Playoff, Points, Format);
        var round = AddRound(tournament, divisionId);
        tournament.AddStageGroup(round.Id, "Группа A", [Alpha], stageHasMatches: false);

        Assert.Throws<DomainException>(() =>
            tournament.AddStageGroup(playoff.Id, "Группа A", [Beta], stageHasMatches: false));
        Assert.Throws<DomainException>(() => tournament.UpdateStage(
            round.Id, round.Name, StageFormatType.Playoff, Points, Format, null, stageHasMatches: false));
    }

    [Fact]
    public void Groups_WithMatches_TeamsCanOnlyBeAdded()
    {
        var tournament = CreateTournament();
        var stage = AddRound(tournament, tournament.Divisions.Single().Id);
        var group = tournament.AddStageGroup(stage.Id, "Группа A", [Alpha, Beta], stageHasMatches: false);

        tournament.UpdateStageGroup(stage.Id, group.Id, "Группа A", [Alpha, Beta, Gamma], stageHasMatches: true);

        Assert.Equal(3, group.TeamIds.Count);
        Assert.Throws<ConflictException>(() =>
            tournament.UpdateStageGroup(stage.Id, group.Id, "Группа A", [Alpha], stageHasMatches: true));
        Assert.Throws<ConflictException>(() =>
            tournament.RemoveStageGroup(stage.Id, group.Id, stageHasMatches: true));
    }

    [Fact]
    public void Groups_FirstGroupAfterMatches_Conflict()
    {
        var tournament = CreateTournament();
        var stage = AddRound(tournament, tournament.Divisions.Single().Id);

        Assert.Throws<ConflictException>(() =>
            tournament.AddStageGroup(stage.Id, "Группа A", [Alpha], stageHasMatches: true));
    }

    [Fact]
    public void MatchGroup_ResolvedFromTeams()
    {
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();
        var groups = new List<(Guid, IReadOnlyCollection<Guid>)>
        {
            (groupA, [Alpha, Beta]),
            (groupB, [Gamma, Delta])
        };

        Assert.Null(MatchGroupResolver.Resolve([], Alpha, Gamma));
        Assert.Equal(groupB, MatchGroupResolver.Resolve(groups, Delta, Gamma));
        Assert.Throws<DomainException>(() => MatchGroupResolver.Resolve(groups, Alpha, Gamma));
        Assert.Throws<DomainException>(() => MatchGroupResolver.Resolve(groups, Alpha, Guid.NewGuid()));
    }

    [Fact]
    public void Standings_PerGroup_WithQualificationZone()
    {
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();
        var rules = new StandingsRules(
            WinPoints: 3,
            DrawPoints: 1,
            LossPoints: 0,
            Tiebreakers: [StandingsTiebreaker.GoalDifference],
            QualifiersCount: 1,
            Groups:
            [
                new StandingsGroup(groupB, "Группа B", 2, [Gamma, Delta]),
                new StandingsGroup(groupA, "Группа A", 1, [Alpha, Beta])
            ]);

        var tables = StandingsCalculator.CalculateTables(
            [new StandingsMatch(Alpha, Beta, 0, 2, groupA)],
            rules);

        Assert.Equal(["Группа A", "Группа B"], tables.Select(table => table.GroupName));

        var first = tables[0];
        Assert.Equal([Beta, Alpha], first.Entries.Select(entry => entry.TeamId));
        Assert.True(first.Qualifies(1));
        Assert.False(first.Qualifies(2));

        // Группа без сыгранных матчей всё равно показывает свои команды — с нулями.
        Assert.Equal(2, tables[1].Entries.Count);
        Assert.All(tables[1].Entries, entry => Assert.Equal(0, entry.Played));
    }

    [Fact]
    public void Standings_WithoutGroups_SingleTableWithoutZoneByDefault()
    {
        var table = Assert.Single(StandingsCalculator.CalculateTables(
            [new StandingsMatch(Alpha, Beta, 1, 0)],
            StandingsRules.Default));

        Assert.Null(table.GroupId);
        Assert.False(table.Qualifies(1));
    }
}
