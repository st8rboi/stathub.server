using Stathub.Modules.Teams.Domain.Entities;
using Stathub.Shared.Exceptions;
using Xunit;

namespace Stathub.Modules.Matches.Domain.Tests;

/// <summary>
/// Логотип команды закрепляется за организацией, загрузившей его первой:
/// остальные переиспользуют его или меняют через техподдержку.
/// </summary>
public sealed class TeamLogoTests
{
    private static readonly Guid FirstTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTenant = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void SetLogo_FirstUpload_PinsLogoToTenant()
    {
        var team = Team.Create("ФК Тест");

        team.SetLogo("/uploads/logos/a.png", FirstTenant);

        Assert.Equal("/uploads/logos/a.png", team.LogoUrl);
        Assert.Equal(FirstTenant, team.LogoOwnerTenantId);
    }

    [Fact]
    public void SetLogo_OwnerCanReplaceOwnLogo()
    {
        var team = Team.Create("ФК Тест");
        team.SetLogo("/uploads/logos/a.png", FirstTenant);

        team.SetLogo("/uploads/logos/b.png", FirstTenant);

        Assert.Equal("/uploads/logos/b.png", team.LogoUrl);
    }

    [Theory]
    [InlineData("/uploads/logos/b.png")]
    [InlineData(null)]
    public void SetLogo_OtherTenantCannotChangeLogo(string? logoUrl)
    {
        var team = Team.Create("ФК Тест");
        team.SetLogo("/uploads/logos/a.png", FirstTenant);

        Assert.Throws<ForbiddenException>(() => team.SetLogo(logoUrl, OtherTenant));

        Assert.Equal("/uploads/logos/a.png", team.LogoUrl);
        Assert.Equal(FirstTenant, team.LogoOwnerTenantId);
    }

    [Fact]
    public void SetLogo_AfterOwnerRemovesLogo_AnyTenantCanUpload()
    {
        var team = Team.Create("ФК Тест");
        team.SetLogo("/uploads/logos/a.png", FirstTenant);
        team.SetLogo(null, FirstTenant);

        team.SetLogo("/uploads/logos/b.png", OtherTenant);

        Assert.Equal(OtherTenant, team.LogoOwnerTenantId);
    }

    [Fact]
    public void ResetLogo_ReleasesLogoForAnyTenant()
    {
        var team = Team.Create("ФК Тест");
        team.SetLogo("/uploads/logos/a.png", FirstTenant);

        team.ResetLogo();
        team.SetLogo("/uploads/logos/b.png", OtherTenant);

        Assert.Equal(OtherTenant, team.LogoOwnerTenantId);
    }
}
