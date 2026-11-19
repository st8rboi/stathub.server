namespace Stathub.Modules.Statistics.Application;

/// <summary>
/// Подпись игрока на витрине: «Фамилия Имя», без отчества. Правило для всей публичной части
/// (статистика, протокол, состав, профиль): полное ФИО видно только в админке организатора.
/// </summary>
public static class PlayerDisplayName
{
    public static string ToDisplay(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "Игрок";

        var parts = fullName.Split(
            [' ', '\t', '\u00A0'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            0 => "Игрок",
            1 => parts[0],
            _ => $"{parts[0]} {parts[1]}"
        };
    }
}
