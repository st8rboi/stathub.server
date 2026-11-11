using Stathub.Modules.Players.Domain.Entities;
using Stathub.Shared.Exceptions;
using Xunit;

namespace Stathub.Modules.Matches.Domain.Tests;

/// <summary>
/// Фото игрока, как и логотип команды, закрепляется за организацией, загрузившей его первой:
/// остальные переиспользуют его или меняют через техподдержку.
/// </summary>
public sealed class PlayerPhotoTests
{
    private static readonly Guid FirstTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTenant = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void SetPhoto_FirstUpload_PinsPhotoToTenant()
    {
        var player = Player.Create("Иванов Иван");

        player.SetPhoto("/uploads/logos/a.jpg", FirstTenant);

        Assert.Equal("/uploads/logos/a.jpg", player.PhotoUrl);
        Assert.Equal(FirstTenant, player.PhotoOwnerTenantId);
    }

    [Fact]
    public void SetPhoto_OwnerCanReplaceOwnPhoto()
    {
        var player = Player.Create("Иванов Иван");
        player.SetPhoto("/uploads/logos/a.jpg", FirstTenant);

        player.SetPhoto("/uploads/logos/b.jpg", FirstTenant);

        Assert.Equal("/uploads/logos/b.jpg", player.PhotoUrl);
    }

    [Theory]
    [InlineData("/uploads/logos/b.jpg")]
    [InlineData(null)]
    public void SetPhoto_OtherTenantCannotChangePhoto(string? photoUrl)
    {
        var player = Player.Create("Иванов Иван");
        player.SetPhoto("/uploads/logos/a.jpg", FirstTenant);

        Assert.Throws<ForbiddenException>(() => player.SetPhoto(photoUrl, OtherTenant));

        Assert.Equal("/uploads/logos/a.jpg", player.PhotoUrl);
        Assert.Equal(FirstTenant, player.PhotoOwnerTenantId);
    }

    [Fact]
    public void SetPhoto_AfterOwnerRemovesPhoto_AnyTenantCanUpload()
    {
        var player = Player.Create("Иванов Иван");
        player.SetPhoto("/uploads/logos/a.jpg", FirstTenant);
        player.SetPhoto(null, FirstTenant);

        player.SetPhoto("/uploads/logos/b.jpg", OtherTenant);

        Assert.Equal(OtherTenant, player.PhotoOwnerTenantId);
    }

    [Fact]
    public void SetPhoto_TooLongPath_Rejected()
    {
        var player = Player.Create("Иванов Иван");

        Assert.Throws<DomainException>(() => player.SetPhoto(new string('a', 501), FirstTenant));
    }

    [Fact]
    public void ResetPhoto_ReleasesPhotoForAnyTenant()
    {
        var player = Player.Create("Иванов Иван");
        player.SetPhoto("/uploads/logos/a.jpg", FirstTenant);

        player.ResetPhoto();
        player.SetPhoto("/uploads/logos/b.jpg", OtherTenant);

        Assert.Equal(OtherTenant, player.PhotoOwnerTenantId);
    }
}
