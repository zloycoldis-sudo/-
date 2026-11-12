using System.Text.Json.Serialization;

namespace Hvylia.Api.Models;

/// <summary>Погодинний запис (аркуш «Погодинно»).</summary>
public sealed class HourlyRecord
{
    public DateTime Date { get; set; }
    public string DayOfWeek { get; set; } = "";
    public int Hour { get; set; }
    public int Works { get; set; }                 // працює (0/1)
    public int DemandCars { get; set; }            // попит_авто
    public double ConsumptionKwh { get; set; }     // споживання_кВт·год (після санітарії)
    public int GridAvailable { get; set; } = 1;    // мережа_є (0/1)
    public decimal RdnPrice { get; set; }          // ціна_РДН_грн_за_МВт·год
    public double Temperature { get; set; }        // температура_°C

    [JsonIgnore] public DateTime Timestamp => Date.Date.AddHours(Hour);
}

/// <summary>Обладнання (аркуш «Обладнання»).</summary>
public sealed class EquipmentItem
{
    public string Name { get; set; } = "";
    public double RatedPowerKw { get; set; }
    public double StartingFactor { get; set; } = 1; // коефіцієнт пуску (R4); 1 = без пуску
    public int Quantity { get; set; } = 1;
}

public sealed class Dataset
{
    public List<HourlyRecord> Hourly { get; set; } = new();
    public List<EquipmentItem> Equipment { get; set; } = new();
}

/// <summary>Налаштування розрахунків (appsettings.json → "Calc"). Значення за замовчуванням — ПЛЕЙСХОЛДЕРИ.</summary>
public sealed class CalcSettings
{
    public DateTime YearStart { get; set; } = new(2025, 11, 1);
    public DateTime YearEnd { get; set; } = new(2026, 10, 31);

    /// <summary>Тариф розподілу/постачання, грн/кВт·год (на додачу до РДН). TODO: з паспорта/довідки.</summary>
    public double DistributionTariffUahPerKwh { get; set; } = 0;

    /// <summary>Стандартний ряд потужностей генератора, кВт.</summary>
    public double[] GeneratorStandardSizesKw { get; set; } = { 10, 15, 20, 30, 40, 50, 60, 80, 100 };

    // Лінійка витрат дизеля (TODO: взяти з паспорта генератора)
    public double IdleFuelLitersPerHourPerKw { get; set; } = 0.05; // холостий хід, л/год на 1 кВт номіналу
    public double FuelLitersPerKwh { get; set; } = 0.30;           // л на вироблену кВт·год
    public double MaintenanceUahPerRunHour { get; set; } = 0;      // ТО, грн за годину роботи
}

/// <summary>Запит для «прихованого тижня» (показники №4–№6).</summary>
public sealed class HiddenWeekRequest
{
    public DateTime WeekStart { get; set; } = new(2026, 1, 19);
    public DateTime WeekEnd { get; set; } = new(2026, 1, 25);

    /// <summary>Графік «4 через 4»: 4 год світло, 4 год відключення.</summary>
    public int OnHours { get; set; } = 4;
    public int OffHours { get; set; } = 4;
    /// <summary>TODO: уточнити в умові, з чого починається доба (з відключення чи зі світла).</summary>
    public bool FirstBlockIsOutage { get; set; } = false;

    public double DieselPriceUahPerLiter { get; set; } = 65;
    public double MarginPerCarUah { get; set; } = 340;

    /// <summary>Потужність генератора, кВт. Якщо null — береться результат R4 (показник №3).</summary>
    public double? GeneratorKw { get; set; }
}

// ---------- Результати ----------

public sealed record GeneratorSizing(double RunningKw, double StartupPeakKw, double PeakKw, double SelectedKw);

/// <summary>Показники №1–№3.</summary>
public sealed record BaselineResult(
    double GridCostUah,        // №1
    double UnservedCars,       // №2
    GeneratorSizing Generator  // №3 (R4)
);

/// <summary>Показники №4–№6.</summary>
public sealed record HiddenWeekResult(
    double GeneratorKw,
    int OutageHours,
    double FuelLiters,         // №4
    double FuelCostUah,
    double MaintenanceUah,
    double ServedCars,
    double CostPerCarUah,      // №5
    double NetMarginUah        // №6
);

public sealed record StatusResponse(bool HasData, int HourlyRows, int EquipmentItems, DateTime? From, DateTime? To);
