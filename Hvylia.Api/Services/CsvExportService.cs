using System.Globalization;
using System.Text;
using Hvylia.Api.Models;

namespace Hvylia.Api.Services;

/// <summary>team_&lt;N&gt;_відповідь.csv — UTF-8 з BOM, роздільник «;», десяткова кома.</summary>
public sealed class CsvExportService
{
    private static readonly CultureInfo Uk = new("uk-UA");

    public (byte[] Bytes, string FileName) Build(BaselineResult b, HiddenWeekResult h, string team)
    {
        // TODO: назви показників і порядок — за шаблоном відповіді з умови.
        var sb = new StringBuilder();
        sb.AppendLine("показник;значення");
        sb.AppendLine($"1_витрати_мережа_грн;{F(b.GridCostUah)}");
        sb.AppendLine($"2_необслужені_авто;{F(b.UnservedCars)}");
        sb.AppendLine($"3_генератор_R4_кВт;{F(b.Generator.SelectedKw)}");
        sb.AppendLine($"4_пальне_л;{F(h.FuelLiters)}");
        sb.AppendLine($"5_собівартість_авто_грн;{F(h.CostPerCarUah)}");
        sb.AppendLine($"6_маржа_мінус_генератор_грн;{F(h.NetMarginUah)}");

        var bom = new UTF8Encoding(true).GetPreamble();
        var bytes = bom.Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return (bytes, $"team_{team}_відповідь.csv");
    }

    private static string F(double v) => v.ToString("0.##", Uk);
}
