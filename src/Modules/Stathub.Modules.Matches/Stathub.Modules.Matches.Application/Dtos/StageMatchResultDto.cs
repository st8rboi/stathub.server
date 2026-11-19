namespace Stathub.Modules.Matches.Application.Dtos;

/// <summary>
/// Результат матча этапа без доменных типов Matches: нужен расчёту таблицы,
/// который не должен знать про статусы и правила матча.
/// </summary>
/// <param name="GroupId">Группа этапа, в которой сыгран матч; null — этап без групп.</param>
/// <param name="IsLive">Матч ещё идёт (в том числе перерыв): счёт текущий, а не итоговый.</param>
public sealed record StageMatchResultDto(
    Guid MatchId,
    Guid HomeTeamId,
    Guid AwayTeamId,
    int HomeScore,
    int AwayScore,
    Guid? GroupId,
    bool IsLive);
