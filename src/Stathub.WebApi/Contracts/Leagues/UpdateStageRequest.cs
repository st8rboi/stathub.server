using Stathub.Modules.Leagues.Domain.Enums;

namespace Stathub.WebApi.Contracts.Leagues;

/// <summary>
/// Изменение этапа. Передаётся полное состояние: название меняется всегда, а правила —
/// только пока турнир не начат и в этапе нет матчей (иначе 409, если правила отличаются).
/// </summary>
public sealed record UpdateStageRequest(
    string Name,
    StageFormatType FormatType,
    int WinPoints,
    int DrawPoints,
    int LossPoints,
    int PeriodDurationMinutes,
    int PeriodsCount,
    bool ExtraTimeEnabled,
    bool PenaltyShootoutEnabled);
