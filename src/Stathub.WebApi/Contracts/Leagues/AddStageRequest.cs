using Stathub.Modules.Leagues.Domain.Enums;

namespace Stathub.WebApi.Contracts.Leagues;

/// <summary>
/// Запрос на добавление нового этапа в дивизион турнира.
/// </summary>
/// <param name="DivisionId">Дивизион этапа; можно не указывать, пока дивизион в турнире один.</param>
/// <param name="QualifiersCount">
/// Сколько первых команд выходит в следующий этап (при группах — из каждой группы); только у кругового этапа.
/// </param>
public sealed record AddStageRequest(
    string Name,
    StageFormatType FormatType,
    int WinPoints,
    int DrawPoints,
    int LossPoints,
    int PeriodDurationMinutes,
    int PeriodsCount,
    bool ExtraTimeEnabled,
    bool PenaltyShootoutEnabled,
    Guid? DivisionId = null,
    int? QualifiersCount = null);
