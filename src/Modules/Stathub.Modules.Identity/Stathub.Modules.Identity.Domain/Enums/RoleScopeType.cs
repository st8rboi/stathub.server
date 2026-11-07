namespace Stathub.Modules.Identity.Domain.Enums;

/// <summary>
/// На что выдана роль: организация целиком, лига, турнир, команда или конкретный матч.
/// Именно скоуп, а не роль в токене, отвечает на вопрос «какой матч может вести этот судья».
/// </summary>
public enum RoleScopeType
{
    Tenant = 0,
    League = 1,
    Tournament = 2,
    Team = 3,
    Match = 4
}
