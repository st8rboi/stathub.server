using Stathub.WebApi.Contracts.Public;
using Stathub.WebApi.Infrastructure.Export;

namespace Stathub.WebApi.Infrastructure.Public;

/// <summary>
/// Полная статистика игроков дивизиона в .xlsx: только текст и числа, без фото и логотипов.
/// На витрине показывается топ-10, а всё остальное — в этом файле.
/// </summary>
public static class StatisticsWorkbook
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static (string FileName, byte[] Content) Build(PublicTournamentStatisticsDto statistics, DateTime generatedAtUtc)
    {
        var tournament = statistics.Tournament;
        var division = statistics.Divisions.Count > 1
            ? statistics.Divisions.FirstOrDefault(item => item.Id == statistics.DivisionId)?.Name
            : null;

        var title = string.Join(" · ", new[] { tournament.LeagueName, tournament.Name, division }.Where(part => !string.IsNullOrWhiteSpace(part)));

        // Тот же порядок, что у «Бомбардиров» на витрине: голы, затем гол+пас, затем фамилия.
        var rows = statistics.Rows
            .OrderByDescending(row => row.Goals)
            .ThenByDescending(row => row.Goals + row.Assists)
            // Культур нет (InvariantGlobalization), а для кириллицы порядок кодов совпадает с алфавитом.
            .ThenBy(row => row.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var writer = new XlsxWriter()
            .SetColumnWidths(5, 36, 32, 6, 6, 6, 7, 6, 6, 6)
            .AddRow([$"Статистика игроков: {title}"], bold: true)
            .AddRow([$"Сформировано {generatedAtUtc:dd.MM.yyyy HH:mm} UTC · статхаб.рф"])
            .AddRow([])
            .AddTableHeader(["№", "Игрок", "Команда", "М", "Г", "А", "Г+П", "АГ", "ЖК", "КК"]);

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            writer.AddRow([
                index + 1,
                row.PlayerName,
                row.TeamName ?? string.Empty,
                row.Matches,
                row.Goals,
                row.Assists,
                row.Goals + row.Assists,
                row.OwnGoals,
                row.YellowCards,
                row.RedCards
            ]);
        }

        writer.AddRow([]);
        writer.AddRow(["М — матчи, в которых у игрока было событие; Г — голы; А — передачи; АГ — автоголы; ЖК/КК — карточки. Отменённые события не учитываются."]);

        return ($"{SafeFileName($"Статистика — {title}")}.xlsx", writer.Build("Статистика"));
    }

    /// <summary>
    /// Имя файла, допустимое на любой ОС зрителя: сервер на Linux, а скачивают и в Windows,
    /// поэтому запрещённые символы берём явным списком, а не из Path.GetInvalidFileNameChars().
    /// </summary>
    private static string SafeFileName(string name)
    {
        const string invalid = "\\/:*?\"<>|";
        var clean = new string(name.Select(ch => ch < ' ' || invalid.Contains(ch) ? ' ' : ch).ToArray()).Trim();
        return clean.Length > 120 ? clean[..120].Trim() : clean;
    }
}
