using Hvylia.Api.Models;

namespace Hvylia.Api.Services;

public interface ICalculationService
{
    BaselineResult CalculateBaseline(Dataset ds);
    HiddenWeekResult CalculateHiddenWeek(Dataset ds, HiddenWeekRequest req);
}

/// <summary>Розрахунок показників №1–№6. Чисті функції — без побічних ефектів.</summary>
public sealed class CalculationService : ICalculationService
{
    private readonly CalcSettings _s;
    public CalculationService(CalcSettings settings) => _s = settings;

    // ---------- №1–№3 ----------
    public BaselineResult CalculateBaseline(Dataset ds)
    {
        var year = ds.Hourly.Where(r => r.Date >= _s.YearStart && r.Date <= _s.YearEnd).ToList();

        // №1: витрати на мережу за рік (без резерву): kWh × (РДН/1000) + тариф розподілу
        var gridCost = year.Where(r => r.GridAvailable == 1)
            .Sum(r => r.ConsumptionKwh * ((double)r.RdnPrice / 1000.0 + _s.DistributionTariffUahPerKwh));

        // №2: авто, не обслуговані через відключення (працює=1, мережі немає)
        var unserved = year.Where(r => r.Works == 1 && r.GridAvailable == 0).Sum(r => (double)r.DemandCars);

        // №3: генератор за правилом R4
        return new BaselineResult(gridCost, unserved, SizeGenerator(ds.Equipment));
    }

    /// <summary>
    /// R4 (ПРИПУЩЕННЯ — звірити з умовою): піковe навантаження = Σ(P·N) + найбільша додаткова пускова
    /// складова одного двигуна P·(k−1) (двигуни стартують послідовно); далі найближчий стандартний типорозмір.
    /// </summary>
    public GeneratorSizing SizeGenerator(IReadOnlyList<EquipmentItem> equipment)
    {
        var running = equipment.Sum(e => e.RatedPowerKw * e.Quantity);
        var startupExtra = equipment.Where(e => e.Quantity > 0 && e.StartingFactor > 1)
            .Select(e => e.RatedPowerKw * (e.StartingFactor - 1))
            .DefaultIfEmpty(0).Max();
        var peak = running + startupExtra;

        var selected = _s.GeneratorStandardSizesKw.Where(x => x >= peak)
            .DefaultIfEmpty(Math.Ceiling(peak)).Min();
        return new GeneratorSizing(running, running + startupExtra, peak, selected);
    }

    // ---------- №4–№6 ----------
    public HiddenWeekResult CalculateHiddenWeek(Dataset ds, HiddenWeekRequest req)
    {
        var genKw = req.GeneratorKw ?? SizeGenerator(ds.Equipment).SelectedKw;
        var from = req.WeekStart.Date;
        var to = req.WeekEnd.Date.AddDays(1);

        double fuel = 0, cars = 0;
        var outageHours = 0;

        foreach (var r in ds.Hourly.Where(r => r.Timestamp >= from && r.Timestamp < to))
        {
            if (!IsOutage(r.Timestamp, req)) continue;

            // Генератор працює у відключення, коли мийка працює або потрібен підігрів (T<0).
            // TODO: уточнити в умові, чи генератор вмикається вночі.
            if (r.Works == 0 && r.Temperature >= 0) continue;

            outageHours++;
            fuel += _s.IdleFuelLitersPerHourPerKw * genKw + _s.FuelLitersPerKwh * r.ConsumptionKwh;
            if (r.Works == 1) cars += r.DemandCars; // TODO: обмеження пропускною здатністю постів/генератора
        }

        var fuelCost = fuel * req.DieselPriceUahPerLiter;
        var maintenance = outageHours * _s.MaintenanceUahPerRunHour;
        var costPerCar = cars > 0 ? (fuelCost + maintenance) / cars : 0;          // №5
        var net = cars * req.MarginPerCarUah - (fuelCost + maintenance);          // №6

        return new HiddenWeekResult(genKw, outageHours, fuel, fuelCost, maintenance, cars, costPerCar, net);
    }

    /// <summary>Графік «N через M»: відлік від 00:00 першого дня тижня.</summary>
    public static bool IsOutage(DateTime ts, HiddenWeekRequest req)
    {
        var cycle = req.OnHours + req.OffHours;
        if (cycle <= 0) return false;
        var hours = (int)Math.Floor((ts - req.WeekStart.Date).TotalHours);
        var pos = ((hours % cycle) + cycle) % cycle;
        return req.FirstBlockIsOutage ? pos < req.OffHours : pos >= req.OnHours;
    }
}
