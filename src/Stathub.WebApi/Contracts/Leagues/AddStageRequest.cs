using Stathub.Modules.Leagues.Domain.Enums;

namespace Stathub.WebApi.Contracts.Leagues;

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
