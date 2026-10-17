namespace Stathub.Modules.Statistics.Application;

/// <summary>
/// Подпись игрока на витрине: «Фамилия Имя», без отчества. Отчество в таблицах «топов»
/// только шумит, а полное ФИО приходит в справочнике игроков и протоколе.
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
