using Stathub.Modules.Players.Domain.Entities;

namespace Stathub.Modules.Players.Application.Dtos;

/// <summary>
/// Подсказка при поиске существующего игрока.
/// <para>
/// По documentation/doc.md §9.3 в подсказках нужно показывать не только имя и дату рождения,
/// а ещё и контекст последнего известного участия (команда, лига) — это помогает организатору
/// отличить полного тёзку от искомого человека. Контекст добавится здесь, когда появится
/// PlayerRegistration (после модуля Teams): <c>LastTeamName</c>, <c>LastLeagueName</c>.
/// </para>
/// </summary>
public sealed record PlayerSuggestionDto(
    Guid Id,
    string FullName,
    DateOnly? BirthDate)
{
    internal static PlayerSuggestionDto From(Player player) => new(
        player.Id,
        player.FullName,
        player.BirthDate);
}
