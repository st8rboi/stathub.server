using Stathub.Modules.Finance.Domain.Entities;
using Stathub.Modules.Finance.Domain.Enums;
using Stathub.Modules.Matches.Domain.Entities;
using Stathub.Modules.Matches.Domain.ValueObjects;
using Stathub.Shared.Exceptions;
using Xunit;

namespace Stathub.Modules.Matches.Domain.Tests;

/// <summary>
/// Правка и удаление матча: только не начатый матч без событий. Неоплаченный взнос переходит
/// к новой команде, оплаченный блокирует замену и удаление.
/// </summary>
public sealed class MatchEditTests
{
    private static readonly DateTime Kickoff = new(2026, 6, 1, 15, 0, 0, DateTimeKind.Utc);

    private static Match CreateMatch() => Match.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Kickoff,
        MatchFormat.Create(periodsCount: 2, periodDurationMinutes: 45, extraTimeEnabled: false, penaltyShootoutEnabled: false));

    [Fact]
    public void Update_ScheduledWithoutEvents_ChangesTimeAndTeams()
    {
        var match = CreateMatch();
        var home = Guid.NewGuid();
        var away = Guid.NewGuid();
        var newTime = Kickoff.AddDays(1);

        match.Update(newTime, home, away, groupId: null, hasEvents: false);

        Assert.Equal(newTime, match.ScheduledTimeUtc);
        Assert.Equal(home, match.HomeTeamId);
        Assert.Equal(away, match.AwayTeamId);
    }

    [Fact]
    public void Update_SameTeamTwice_Rejected()
    {
        var match = CreateMatch();
        var team = Guid.NewGuid();

        Assert.Throws<DomainException>(() => match.Update(Kickoff, team, team, groupId: null, hasEvents: false));
    }

    [Fact]
    public void Update_WithEvents_Conflict()
    {
        var match = CreateMatch();

        Assert.Throws<ConflictException>(() =>
            match.Update(Kickoff.AddDays(1), match.HomeTeamId, match.AwayTeamId, groupId: null, hasEvents: true));
    }

    [Fact]
    public void ApplyFormat_ScheduledWithoutEvents_ReplacesRules()
    {
        var match = CreateMatch();

        match.ApplyFormat(
            MatchFormat.Create(periodsCount: 2, periodDurationMinutes: 30, extraTimeEnabled: true, penaltyShootoutEnabled: true, bonusGoalEnabled: true),
            hasEvents: false);

        Assert.Equal(30, match.PeriodDurationMinutes);
        Assert.True(match.ExtraTimeEnabled);
        Assert.True(match.PenaltyShootoutEnabled);
        Assert.True(match.BonusGoalEnabled);
    }

    [Fact]
    public void ApplyFormat_StartedOrWithEvents_Conflict()
    {
        var format = MatchFormat.Create(periodsCount: 2, periodDurationMinutes: 30, extraTimeEnabled: false, penaltyShootoutEnabled: false);

        var withEvents = CreateMatch();
        Assert.Throws<ConflictException>(() => withEvents.ApplyFormat(format, hasEvents: true));

        var started = CreateMatch();
        started.Start(Kickoff);
        Assert.Throws<ConflictException>(() => started.ApplyFormat(format, hasEvents: false));
        Assert.Equal(45, started.PeriodDurationMinutes);
    }

    [Fact]
    public void Update_StartedMatch_Conflict()
    {
        var match = CreateMatch();
        match.Start(Kickoff);

        Assert.Throws<ConflictException>(() =>
            match.Update(Kickoff.AddDays(1), match.HomeTeamId, match.AwayTeamId, groupId: null, hasEvents: false));
    }

    [Fact]
    public void EnsureCanDelete_ScheduledWithoutEvents_Allowed()
    {
        CreateMatch().EnsureCanDelete(hasEvents: false);
    }

    [Fact]
    public void EnsureCanDelete_WithEvents_Conflict()
    {
        Assert.Throws<ConflictException>(() => CreateMatch().EnsureCanDelete(hasEvents: true));
    }

    [Fact]
    public void ChargeChangeTeam_Pending_MovesDebt()
    {
        var charge = MatchCharge.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ChargeSide.Home, 500);
        var newTeam = Guid.NewGuid();

        charge.ChangeTeam(newTeam);

        Assert.Equal(newTeam, charge.TeamId);
    }

    [Fact]
    public void ChargeChangeTeam_Paid_Conflict()
    {
        var charge = MatchCharge.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ChargeSide.Home, 500);
        charge.MarkPaid(PaymentMethod.Cash, null);

        Assert.Throws<ConflictException>(() => charge.ChangeTeam(Guid.NewGuid()));
    }

    [Fact]
    public void ChargeChangeTeam_PaidButSameTeam_Allowed()
    {
        var team = Guid.NewGuid();
        var charge = MatchCharge.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), team, ChargeSide.Home, 500);
        charge.MarkPaid(PaymentMethod.Cash, null);

        charge.ChangeTeam(team);

        Assert.Equal(team, charge.TeamId);
    }

    [Fact]
    public void ChargeEnsureCanRemove_Paid_Conflict()
    {
        var charge = MatchCharge.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ChargeSide.Away, 500);
        charge.MarkPaid(PaymentMethod.Cash, null);

        Assert.Throws<ConflictException>(charge.EnsureCanRemove);
    }
}
