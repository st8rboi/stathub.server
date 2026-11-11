using Stathub.Modules.Rosters.Domain.Entities;
using Stathub.Modules.Rosters.Domain.Enums;
using Stathub.Shared.Exceptions;
using Xunit;

namespace Stathub.Modules.Matches.Domain.Tests;

/// <summary>
/// Удаление заявок: удалить можно только ошибочную заявку без событий — история переходов остаётся (documentation/doc.md §7).
/// </summary>
public sealed class RosterEditTests
{
    private static readonly DateOnly Today = new(2026, 5, 1);

    private static TournamentTeam CreateTeam() =>
        TournamentTeam.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void RemoveRegistration_WithoutEvents_Removes()
    {
        var team = CreateTeam();
        var registration = team.RegisterPlayer(Guid.NewGuid(), Today);

        team.RemoveRegistration(registration.Id, hasMatchEvents: false);

        Assert.Empty(team.Registrations);
    }

    [Fact]
    public void RemoveRegistration_WithEvents_Conflict()
    {
        var team = CreateTeam();
        var registration = team.RegisterPlayer(Guid.NewGuid(), Today);

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
