using Stathub.Modules.Rosters.Domain.Entities;
using Stathub.Modules.Rosters.Domain.Enums;
using Stathub.Shared.Exceptions;
using Xunit;

namespace Stathub.Modules.Matches.Domain.Tests;

/// <summary>
/// Правка и удаление заявок: номер меняется только в активной заявке, удалить можно только
/// ошибочную заявку без событий — история переходов остаётся (documentation/doc.md §7).
/// </summary>
public sealed class RosterEditTests
{
    private static readonly DateOnly Today = new(2026, 5, 1);

    private static TournamentTeam CreateTeam() =>
        TournamentTeam.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void ChangeSquadNumber_FreeNumber_Changes()
    {
        var team = CreateTeam();
        var registration = team.RegisterPlayer(Guid.NewGuid(), 7, Today);

        team.ChangeSquadNumber(registration.Id, 10);

        Assert.Equal(10, registration.SquadNumber);
    }

    [Fact]
    public void ChangeSquadNumber_SameNumber_Allowed()
    {
        var team = CreateTeam();
        var registration = team.RegisterPlayer(Guid.NewGuid(), 7, Today);

        team.ChangeSquadNumber(registration.Id, 7);

        Assert.Equal(7, registration.SquadNumber);
    }

    [Fact]
    public void ChangeSquadNumber_TakenByActiveTeammate_Conflict()
    {
        var team = CreateTeam();
        team.RegisterPlayer(Guid.NewGuid(), 10, Today);
        var registration = team.RegisterPlayer(Guid.NewGuid(), 7, Today);

        Assert.Throws<ConflictException>(() => team.ChangeSquadNumber(registration.Id, 10));
    }

    [Fact]
    public void ChangeSquadNumber_TakenByReleasedPlayer_Allowed()
    {
        var team = CreateTeam();
        var released = team.RegisterPlayer(Guid.NewGuid(), 10, Today);
        team.ReleasePlayer(released.Id, Today);
        var registration = team.RegisterPlayer(Guid.NewGuid(), 7, Today);

        team.ChangeSquadNumber(registration.Id, 10);

        Assert.Equal(10, registration.SquadNumber);
    }

    [Fact]
    public void ChangeSquadNumber_ClosedRegistration_Conflict()
    {
        var team = CreateTeam();
        var registration = team.RegisterPlayer(Guid.NewGuid(), 7, Today);
        team.ReleasePlayer(registration.Id, Today);

        Assert.Throws<ConflictException>(() => team.ChangeSquadNumber(registration.Id, 8));
    }

    [Fact]
    public void ChangeSquadNumber_OutOfRange_Rejected()
    {
        var team = CreateTeam();
        var registration = team.RegisterPlayer(Guid.NewGuid(), 7, Today);

        Assert.Throws<DomainException>(() => team.ChangeSquadNumber(registration.Id, 0));
    }

    [Fact]
    public void RemoveRegistration_WithoutEvents_Removes()
    {
        var team = CreateTeam();
        var registration = team.RegisterPlayer(Guid.NewGuid(), 7, Today);

        team.RemoveRegistration(registration.Id, hasMatchEvents: false);

        Assert.Empty(team.Registrations);
    }

    [Fact]
    public void RemoveRegistration_WithEvents_Conflict()
    {
        var team = CreateTeam();
        var registration = team.RegisterPlayer(Guid.NewGuid(), 7, Today);

        Assert.Throws<ConflictException>(() => team.RemoveRegistration(registration.Id, hasMatchEvents: true));
        Assert.Equal(PlayerRegistrationStatus.Active, Assert.Single(team.Registrations).Status);
    }

    [Fact]
    public void DeleteTeam_WithMatches_Conflict()
    {
        Assert.Throws<ConflictException>(() => TournamentTeam.EnsureCanDelete(2));
        TournamentTeam.EnsureCanDelete(0);
    }
}
