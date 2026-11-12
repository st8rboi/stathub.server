namespace Stathub.Modules.Matches.Application.Dtos;

/// <summary>
/// Результат завершённого матча без доменных типов Matches: нужен расчёту таблицы,
/// который не должен знать про статусы и правила матча.
/// </summary>
/// <param name="GroupId">Группа этапа, в которой сыгран матч; null — этап без групп.</param>
public sealed record FinishedMatchDto(
    Guid HomeTeamId,
    Guid AwayTeamId,
    int HomeScore,
    int AwayScore,
    Guid? GroupId);
