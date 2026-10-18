namespace Stathub.WebApi.Infrastructure.Auth;

/// <summary>Имена политик авторизации: одна точка правды для контроллеров и Program.</summary>
public static class AuthPolicies
{
    /// <summary>Любой пользователь с активной организацией в токене.</summary>
    public const string TenantMember = "TenantMember";

    /// <summary>Организатор и администратор организации: полный доступ к её контенту.</summary>
    public const string Organizer = "Organizer";

    /// <summary>Судья: доступ к назначенным матчам (проверка скоупа — в Application).</summary>
    public const string Referee = "Referee";
}
