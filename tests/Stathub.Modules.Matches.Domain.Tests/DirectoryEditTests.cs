using Stathub.Modules.Players.Domain.Entities;
using Stathub.Modules.Teams.Domain.Entities;
using Stathub.Shared.Exceptions;
using Xunit;

namespace Stathub.Modules.Matches.Domain.Tests;

/// <summary>
/// Команды и игроки общие для всех организаторов. Как и с логотипом, править запись может
/// только организатор, в чьих лигах она одна используется; иначе — через техподдержку.
/// </summary>
public sealed class DirectoryEditTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTenant = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void TeamRename_UnusedTeam_AnyOrganizerCanEdit()
    {
        var team = Team.Create("ФК Тест");

        team.Rename("  ФК Новый  ", "  Казань ", Tenant, []);

        Assert.Equal("ФК Новый", team.Name);
        Assert.Equal("Казань", team.City);
    }

    [Fact]
    public void TeamRename_UsedOnlyByOwnLeagues_Allowed()
    {
        var team = Team.Create("ФК Тест", "Казань");

        team.Rename("ФК Тест", null, Tenant, [Tenant]);

        Assert.Null(team.City);
    }

    [Fact]
    public void TeamRename_UsedByOtherOrganizer_Forbidden()
    {
        var team = Team.Create("ФК Тест");

        Assert.Throws<ForbiddenException>(() => team.Rename("ФК Новый", null, Tenant, [Tenant, OtherTenant]));
        Assert.Equal("ФК Тест", team.Name);
    }

    [Fact]
    public void TeamRename_EmptyName_Rejected()
    {
        var team = Team.Create("ФК Тест");

        Assert.Throws<DomainException>(() => team.Rename("  ", null, Tenant, []));
    }

    [Fact]
    public void PlayerUpdate_UsedOnlyByOwnLeagues_ChangesNameAndBirthDate()
    {
        var player = Player.Create("Иванов Иван");
        var birthDate = new DateOnly(2000, 5, 1);

        player.Update(" Иванов Пётр ", birthDate, Tenant, [Tenant]);

        Assert.Equal("Иванов Пётр", player.FullName);
        Assert.Equal(birthDate, player.BirthDate);
    }

    [Fact]
    public void PlayerUpdate_CanClearBirthDate()
    {
        var player = Player.Create("Иванов Иван", new DateOnly(2000, 5, 1));

        player.Update("Иванов Иван", null, Tenant, []);

        Assert.Null(player.BirthDate);
    }

    [Fact]
    public void PlayerUpdate_UsedByOtherOrganizer_Forbidden()
    {
        var player = Player.Create("Иванов Иван");

        Assert.Throws<ForbiddenException>(() => player.Update("Петров Пётр", null, Tenant, [OtherTenant]));
        Assert.Equal("Иванов Иван", player.FullName);
    }

    [Fact]
    public void PlayerUpdate_BirthDateInFuture_Rejected()
    {
        var player = Player.Create("Иванов Иван");
        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        Assert.Throws<DomainException>(() => player.Update("Иванов Иван", future, Tenant, []));
    }
}
