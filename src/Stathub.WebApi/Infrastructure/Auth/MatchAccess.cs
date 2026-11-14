namespace Stathub.WebApi.Infrastructure.Auth;

/// <summary>Что пользователь может делать с протоколом доступного ему матча.</summary>
public enum MatchAccess
{
    /// <summary>Организатор/админ или судья матча: все события, включая жизненный цикл.</summary>
    Full = 0,

    /// <summary>Статистик матча: только удары, фолы, угловые и их отмена.</summary>
    StatisticsOnly = 1
}
