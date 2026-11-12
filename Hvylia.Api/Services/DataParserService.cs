using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Hvylia.Api.Models;

namespace Hvylia.Api.Services;

/// <summary>Читання .csv / .xlsx + «санітарія» даних.</summary>
public sealed class DataParserService
{
    private static readonly CultureInfo Uk = new("uk-UA");

    /// <summary>
    /// Поріг аномалії: якщо мийка не працює (works==0) і значення більше за це — збита кома (385 → 0.385).
    /// У Gemini-чаті 10 кВт, у docx-описі 50. Пайплайн підігріву (~2.4) лишається < обох порогів.
    /// </summary>
    public const double NightAnomalyThresholdKw = 10.0;

    // Ключі для пошуку колонок за підрядком у заголовку (нечутливо до регістру)
    private static readonly string[] Keys =
        { "дата", "день", "година", "працює", "попит", "споживання", "мережа", "ціна", "температура" };

    public Dataset Parse(Stream stream, string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".csv"  => ParseCsv(stream),
            ".xlsx" => ParseXlsx(stream),
            var e   => throw new NotSupportedException($"Непідтримуваний формат: {e}")
        };

    // ---------------- CSV (';' + десяткова кома) ----------------
    public Dataset ParseCsv(Stream stream)
    {
        var cfg = new CsvConfiguration(Uk) { Delimiter = ";", HasHeaderRecord = true, BadDataFound = null };
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, cfg);

        csv.Read(); csv.ReadHeader();
        var map = MapHeaders(csv.HeaderRecord!);
        var ds = new Dataset();

        while (csv.Read())
        {
            ds.Hourly.Add(BuildRecord(key => csv.GetField(map[key]) ?? ""));
        }
        return ds;
    }

    // ---------------- XLSX (ClosedXML) ----------------
    public Dataset ParseXlsx(Stream stream, string hourlySheet = "Погодинно", string equipmentSheet = "Обладнання")
    {
        using var wb = new XLWorkbook(stream);
        var ds = new Dataset();

        var ws = wb.Worksheet(hourlySheet);

        // шукаємо рядок заголовків у перших 10 рядках (у файлі він може бути на 3-му)
        IXLRow? headerRow = null;
        for (var r = 1; r <= 10 && headerRow is null; r++)
        {
            var row = ws.Row(r);
            var texts = row.CellsUsed().Select(c => c.GetString().ToLowerInvariant()).ToList();
            if (texts.Any(t => t.Contains("дата")) && texts.Any(t => t.Contains("працює"))) headerRow = row;
        }
        if (headerRow is null) throw new FormatException("Не знайдено рядок заголовків на аркуші «Погодинно»");

        var lastCol = ws.LastColumnUsed()!.ColumnNumber();
        var headers = Enumerable.Range(1, lastCol).Select(c => headerRow.Cell(c).GetString()).ToList();
        var map = MapHeaders(headers);

        foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            string Get(string key) => CellText(row.Cell(map[key] + 1));
            if (string.IsNullOrWhiteSpace(Get("дата"))) continue;
            ds.Hourly.Add(BuildRecord(Get));
        }

        if (wb.TryGetWorksheet(equipmentSheet, out var eq)) ds.Equipment = ReadEquipment(eq);
        return ds;
    }

    /// <summary>TODO: звірити реальні заголовки аркуша «Обладнання» (тут шукаємо за підрядками).</summary>
    private static List<EquipmentItem> ReadEquipment(IXLWorksheet ws)
    {
        var list = new List<EquipmentItem>();
        var hdrRow = ws.RowsUsed().FirstOrDefault(r =>
            r.CellsUsed().Any(c => c.GetString().ToLowerInvariant().Contains("потужн")));
        if (hdrRow is null) return list;

        int Find(string part) => hdrRow.CellsUsed()
            .FirstOrDefault(c => c.GetString().ToLowerInvariant().Contains(part))?.Address.ColumnNumber ?? -1;

        int cName = Find("назва") > 0 ? Find("назва") : Find("обладнання");
        int cPow = Find("потужн"), cStart = Find("пуск"), cQty = Find("кільк");
        if (cName < 1) cName = 1;

        foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > hdrRow.RowNumber()))
        {
            var name = row.Cell(cName).GetString();
            if (string.IsNullOrWhiteSpace(name) || cPow < 1) continue;
            list.Add(new EquipmentItem
            {
                Name = name,
                RatedPowerKw = ParseDouble(CellText(row.Cell(cPow))),
                StartingFactor = cStart > 0 ? Math.Max(1, ParseDouble(CellText(row.Cell(cStart)))) : 1,
                Quantity = cQty > 0 ? Math.Max(1, (int)ParseDouble(CellText(row.Cell(cQty)))) : 1
            });
        }
        return list;
    }

    // ---------------- Спільне ----------------

    private static HourlyRecord BuildRecord(Func<string, string> get)
    {
        var works = (int)ParseDouble(get("працює"));
        return new HourlyRecord
        {
            Date = ParseDate(get("дата")),
            DayOfWeek = get("день"),
            Hour = (int)ParseDouble(get("година")),
            Works = works,
            DemandCars = (int)ParseDouble(get("попит")),
            ConsumptionKwh = CleanConsumption(get("споживання"), works),
            GridAvailable = (int)ParseDouble(get("мережа")),
            RdnPrice = (decimal)ParseDouble(get("ціна")),
            Temperature = ParseDouble(get("температура"))
        };
    }

    /// <summary>Санітарія: нічна аномалія масштабу (works==0, значення > порогу → ділимо на 1000).</summary>
    public static double CleanConsumption(string raw, int works)
    {
        if (!TryParseDouble(raw, out var val)) return 0.4; // дефолтне базове споживання
        return works == 0 && val > NightAnomalyThresholdKw ? val / 1000.0 : val;
    }

    public static double ParseDouble(string? raw) => TryParseDouble(raw, out var v) ? v : 0;

    private static bool TryParseDouble(string? raw, out double v)
    {
        var s = (raw ?? "").Replace("\u00A0", "").Replace(" ", "").Replace(",", ".");
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);
    }

    private static DateTime ParseDate(string raw) =>
        DateTime.TryParse(raw, Uk, DateTimeStyles.None, out var d) ? d
        : DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d
        : throw new FormatException($"Невірна дата: «{raw}»");

    private static string CellText(IXLCell c) => c.DataType switch
    {
        XLDataType.DateTime => c.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        XLDataType.Number   => c.GetDouble().ToString("R", CultureInfo.InvariantCulture),
        _                   => c.GetString()
    };

    private static Dictionary<string, int> MapHeaders(IReadOnlyList<string> headers)
    {
        var lower = headers.Select(h => h.Trim().ToLowerInvariant()).ToList();
        var map = new Dictionary<string, int>();
        foreach (var key in Keys)
        {
            var idx = lower.FindIndex(h => h.Contains(key));
            if (idx < 0) throw new FormatException($"Не знайдено колонку «{key}»");
            map[key] = idx;
        }
        return map;
    }
}
