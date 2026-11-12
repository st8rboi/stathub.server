namespace Stathub.WebApi.Contracts.Leagues;

/// <summary>Новый дивизион турнира или его переименование.</summary>
public sealed record SaveDivisionRequest(string Name);

/// <summary>
/// Группа кругового этапа: название и полный список её команд (заявленных в дивизион этапа).
/// </summary>
public sealed record SaveStageGroupRequest(string Name, IReadOnlyList<Guid>? TeamIds);
