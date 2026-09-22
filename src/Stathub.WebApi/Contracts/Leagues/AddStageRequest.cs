using Stathub.Modules.Leagues.Domain.Enums;

namespace Stathub.WebApi.Contracts.Leagues;

/// <summary>
/// Запрос на добавление нового этапа в лигу.
/// </summary>
/// <param name="Name"></param>
/// <param name="FormatType"></param>
/// <param name="WinPoints"></param>
/// <param name="DrawPoints"></param>
/// <param name="LossPoints"></param>
/// <param name="PeriodDurationMinutes"></param>
/// <param name="PeriodsCount"></param>
/// <param name="ExtraTimeEnabled"></param>
/// <param name="PenaltyShootoutEnabled"></param>
public sealed record AddStageRequest(
    string Name,
    StageFormatType FormatType,
    int WinPoints,
    int DrawPoints,
    int LossPoints,
    int PeriodDurationMinutes,
    int PeriodsCount,
    bool ExtraTimeEnabled,
    bool PenaltyShootoutEnabled);
