using Stathub.Modules.Finance.Domain.Entities;
using Stathub.Modules.Finance.Domain.Enums;
using Stathub.Shared.Exceptions;
using Xunit;

namespace Stathub.Modules.Matches.Domain.Tests;

/// <summary>
/// Ручные начисления команде (вступительный взнос, штраф, прочее): без матча и стороны,
/// этап необязателен; взнос за матч вручную не выставляется.
/// </summary>
public sealed class ManualChargeTests
{
    [Fact]
    public void CreateManual_EntryFee_WithoutStage()
    {
        var tournament = Guid.NewGuid();
        var team = Guid.NewGuid();

        var charge = MatchCharge.CreateManual(tournament, null, team, ChargeKind.EntryFee, 5000, "  Заявочный взнос  ");

        Assert.Equal(ChargeKind.EntryFee, charge.Kind);
        Assert.Equal(tournament, charge.TournamentId);
        Assert.Equal(team, charge.TeamId);
        Assert.Null(charge.MatchId);
        Assert.Null(charge.StageId);
        Assert.Null(charge.Side);
        Assert.Equal("Заявочный взнос", charge.Description);
        Assert.Equal(ChargeStatus.Pending, charge.Status);
    }

    [Fact]
    public void CreateManual_FineWithStage_KeepsStage()
    {
        var stage = Guid.NewGuid();

        var charge = MatchCharge.CreateManual(Guid.NewGuid(), stage, Guid.NewGuid(), ChargeKind.Fine, 1000, null);

        Assert.Equal(stage, charge.StageId);
        Assert.Null(charge.Description);
    }

    [Fact]
    public void CreateManual_MatchFee_Rejected() =>
        Assert.Throws<DomainException>(() =>
            MatchCharge.CreateManual(Guid.NewGuid(), null, Guid.NewGuid(), ChargeKind.MatchFee, 500, null));

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void CreateManual_NonPositiveAmount_Rejected(int amount) =>
        Assert.Throws<DomainException>(() =>
            MatchCharge.CreateManual(Guid.NewGuid(), null, Guid.NewGuid(), ChargeKind.Fine, amount, null));

    [Fact]
    public void CreateManual_OtherWithoutDescription_Rejected() =>
        Assert.Throws<DomainException>(() =>
            MatchCharge.CreateManual(Guid.NewGuid(), null, Guid.NewGuid(), ChargeKind.Other, 300, "   "));

    [Fact]
    public void CreateManual_TooLongDescription_Rejected() =>
        Assert.Throws<DomainException>(() =>
            MatchCharge.CreateManual(
                Guid.NewGuid(),
                null,
                Guid.NewGuid(),
                ChargeKind.Fine,
                300,
                new string('x', MatchCharge.DescriptionMaxLength + 1)));

    [Fact]
    public void CreateManual_EmptyTeam_Rejected() =>
        Assert.Throws<DomainException>(() =>
            MatchCharge.CreateManual(Guid.NewGuid(), null, Guid.Empty, ChargeKind.Fine, 300, null));

    [Fact]
    public void ManualCharge_PayAndCancelRules_SameAsMatchFee()
    {
        var charge = MatchCharge.CreateManual(Guid.NewGuid(), null, Guid.NewGuid(), ChargeKind.Fine, 1000, null);
        charge.MarkPaid(PaymentMethod.Transfer, null);

        Assert.Equal(ChargeStatus.Paid, charge.Status);
        Assert.Throws<ConflictException>(() => charge.Cancel(null));
    }

    [Fact]
    public void MatchFee_HasKindMatchFee()
    {
        var charge = MatchCharge.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ChargeSide.Home, 500);

        Assert.Equal(ChargeKind.MatchFee, charge.Kind);
        Assert.Null(charge.Description);
    }
}
