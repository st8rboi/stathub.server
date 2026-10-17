namespace Stathub.Modules.Statistics.Application.Dtos;

/// <summary>Строка таблицы статистики игроков на витрине.</summary>
public sealed record PlayerStatisticsRowDto(
    Guid PlayerId,
    string PlayerName,
    Guid? TeamId,
    string? TeamName,
    string? TeamLogoUrl,
    int Matches,
    int Goals,
    int Assists,
    int OwnGoals,
    int YellowCards,
    int RedCards);
