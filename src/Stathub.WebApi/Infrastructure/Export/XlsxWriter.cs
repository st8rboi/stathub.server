using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace Stathub.WebApi.Infrastructure.Export;

/// <summary>
/// Минимальная запись .xlsx (Office Open XML) без сторонних пакетов: один лист, текст и целые числа,
/// жирная строка заголовка таблицы, ширины колонок, закреплённая шапка и автофильтр.
/// Строки пишутся inline-строками (без sharedStrings) — Excel, LibreOffice и Google Sheets это читают.
/// </summary>
public sealed class XlsxWriter
{
    private readonly List<(IReadOnlyList<object?> Cells, bool Bold)> _rows = [];
    private readonly List<double> _widths = [];
    private int? _tableHeaderRow;
    private int _tableColumns;

    /// <summary>Строка над таблицей (заголовок отчёта, подпись), жирная по желанию.</summary>
    public XlsxWriter AddRow(IReadOnlyList<object?> cells, bool bold = false)
    {
        _rows.Add((cells, bold));
        return this;
    }

    /// <summary>Шапка таблицы: жирная, закрепляется при прокрутке, на неё ставится автофильтр.</summary>
    public XlsxWriter AddTableHeader(IReadOnlyList<string> titles)
    {
        _rows.Add((titles.Cast<object?>().ToList(), true));
        _tableHeaderRow = _rows.Count;
        _tableColumns = titles.Count;
        return this;
    }

    /// <summary>Ширины колонок в «символах» Excel, по порядку колонок.</summary>
    public XlsxWriter SetColumnWidths(params double[] widths)
    {
        _widths.Clear();
        _widths.AddRange(widths);
        return this;
    }

    public byte[] Build(string sheetName)
    {
        using var stream = new MemoryStream();

        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml", ContentTypes);
            Write(zip, "_rels/.rels", RootRels);
            Write(zip, "xl/workbook.xml", Workbook(sheetName));
            Write(zip, "xl/_rels/workbook.xml.rels", WorkbookRels);
            Write(zip, "xl/styles.xml", Styles);
            Write(zip, "xl/worksheets/sheet1.xml", Sheet());
        }

        return stream.ToArray();
    }

    private string Sheet()
    {
        var xml = new StringBuilder();
        xml.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        xml.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");

        if (_tableHeaderRow is { } frozen)
        {
            xml.Append("<sheetViews><sheetView workbookViewId=\"0\">");
            xml.Append(CultureInfo.InvariantCulture,
                $"<pane ySplit=\"{frozen}\" topLeftCell=\"A{frozen + 1}\" activePane=\"bottomLeft\" state=\"frozen\"/>");
            xml.Append("</sheetView></sheetViews>");
        }

        if (_widths.Count > 0)
        {
            xml.Append("<cols>");
            for (var index = 0; index < _widths.Count; index++)
            {
                xml.Append(CultureInfo.InvariantCulture,
                    $"<col min=\"{index + 1}\" max=\"{index + 1}\" width=\"{_widths[index]}\" customWidth=\"1\"/>");
            }
            xml.Append("</cols>");
        }

        xml.Append("<sheetData>");

        for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
        {
            var (cells, bold) = _rows[rowIndex];
            var rowNumber = rowIndex + 1;
            xml.Append(CultureInfo.InvariantCulture, $"<row r=\"{rowNumber}\">");

            for (var column = 0; column < cells.Count; column++)
            {
                var reference = ColumnName(column) + rowNumber.ToString(CultureInfo.InvariantCulture);
                var style = bold ? " s=\"1\"" : string.Empty;

                switch (cells[column])
                {
                    case null:
                        break;
                    case int number:
                        xml.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\"{style}><v>{number}</v></c>");
                        break;
                    case var value:
                        xml.Append(CultureInfo.InvariantCulture,
                            $"<c r=\"{reference}\" t=\"inlineStr\"{style}><is><t xml:space=\"preserve\">{Escape(value.ToString())}</t></is></c>");
                        break;
                }
            }

            xml.Append("</row>");
        }

        xml.Append("</sheetData>");

        if (_tableHeaderRow is { } header && _tableColumns > 0 && _rows.Count > header)
        {
            xml.Append(CultureInfo.InvariantCulture,
                $"<autoFilter ref=\"A{header}:{ColumnName(_tableColumns - 1)}{_rows.Count}\"/>");
        }

        xml.Append("</worksheet>");
        return xml.ToString();
    }

    /// <summary>0 → A, 25 → Z, 26 → AA.</summary>
    private static string ColumnName(int index)
    {
        var name = string.Empty;
        for (var value = index + 1; value > 0; value = (value - 1) / 26)
            name = (char)('A' + (value - 1) % 26) + name;
        return name;
    }

    /// <summary>XML-экранирование плюс вырезание управляющих символов, недопустимых в XML 1.0.</summary>
    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var clean = new string(value.Where(ch => ch is '\t' or '\n' or '\r' || ch >= ' ').ToArray());
        return SecurityElement.Escape(clean);
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string Workbook(string sheetName) =>
        $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{Escape(SheetName(sheetName))}" sheetId="1" r:id="rId1"/></sheets></workbook>""";

    /// <summary>Имя листа Excel: до 31 символа и без []:*?/\.</summary>
    private static string SheetName(string name)
    {
        var clean = new string(name.Where(ch => "[]:*?/\\".IndexOf(ch) < 0).ToArray()).Trim();
        if (clean.Length == 0) clean = "Лист1";
        return clean.Length > 31 ? clean[..31] : clean;
    }

    private const string ContentTypes =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""";

    private const string RootRels =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""";

    private const string WorkbookRels =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""";

    // Стиль 0 — обычный, стиль 1 — жирный.
    private const string Styles =
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""";
}
