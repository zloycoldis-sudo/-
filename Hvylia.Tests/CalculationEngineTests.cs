using Hvylia.Api.Models;
using Hvylia.Api.Services;
using Xunit;

namespace Hvylia.Tests;

public class CalculationServiceTests
{
    private static CalculationService Svc(CalcSettings? s = null) => new(s ?? new CalcSettings());

    private static HourlyRecord Rec(DateTime date, int hour, int works = 1, int cars = 0, double kwh = 0,
        int grid = 1, decimal rdn = 0, double temp = 5) =>
        new() { Date = date, Hour = hour, Works = works, DemandCars = cars, ConsumptionKwh = kwh,
                GridAvailable = grid, RdnPrice = rdn, Temperature = temp };

    [Theory]
    [InlineData("385", 0, 0.385)]     // нічна аномалія
    [InlineData("2,4", 0, 2.4)]       // підігрів труб лишається
    [InlineData("120", 1, 120)]       // у робочий час не чіпаємо
    public void Clean_Consumption(string raw, int works, double expected) =>
        Assert.Equal(expected, DataParserService.CleanConsumption(raw, works), 3);

    [Fact]
    public void Baseline_GridCost_OnlyHoursWithGrid()
    {
        var ds = new Dataset();
        ds.Hourly.Add(Rec(new(2026, 1, 10), 10, kwh: 10, grid: 1, rdn: 3000)); // 10 * 3 = 30
        ds.Hourly.Add(Rec(new(2026, 1, 10), 11, kwh: 10, grid: 0, rdn: 3000)); // ігнор
        Assert.Equal(30, Svc().CalculateBaseline(ds).GridCostUah, 3);
    }

    [Fact]
    public void Baseline_UnservedCars_WorkingHoursWithoutGrid()
    {
        var ds = new Dataset();
        ds.Hourly.Add(Rec(new(2026, 1, 10), 10, cars: 3, grid: 0));
        ds.Hourly.Add(Rec(new(2026, 1, 10), 3, works: 0, cars: 5, grid: 0)); // не працює
        ds.Hourly.Add(Rec(new(2026, 1, 10), 11, cars: 4, grid: 1));          // мережа є
        Assert.Equal(3, Svc().CalculateBaseline(ds).UnservedCars);
    }

    [Fact]
    public void R4_Sizing_UsesLargestStartupAndStandardSeries()
    {
        var eq = new List<EquipmentItem>
        {
            new() { Name = "АВД",       RatedPowerKw = 3, StartingFactor = 3, Quantity = 1 },
            new() { Name = "Компресор", RatedPowerKw = 4, StartingFactor = 2, Quantity = 1 },
        };
        var r = Svc().SizeGenerator(eq);
        Assert.Equal(7, r.RunningKw);
        Assert.Equal(13, r.PeakKw);      // 7 + max(3*2, 4*1)
        Assert.Equal(15, r.SelectedKw);  // найближчий із ряду
    }

    [Fact]
    public void FourOnFourOff_Pattern()
    {
        var req = new HiddenWeekRequest();
        Assert.False(CalculationService.IsOutage(new(2026, 1, 19, 0, 0, 0), req));
        Assert.False(CalculationService.IsOutage(new(2026, 1, 19, 3, 0, 0), req));
        Assert.True (CalculationService.IsOutage(new(2026, 1, 19, 4, 0, 0), req));
        Assert.True (CalculationService.IsOutage(new(2026, 1, 19, 7, 0, 0), req));
        Assert.False(CalculationService.IsOutage(new(2026, 1, 19, 8, 0, 0), req));
    }

    [Fact]
    public void HiddenWeek_Fuel_CostPerCar_NetMargin()
    {
        var s = new CalcSettings { IdleFuelLitersPerHourPerKw = 0, FuelLitersPerKwh = 0.3, MaintenanceUahPerRunHour = 0 };
        var ds = new Dataset();
        ds.Hourly.Add(Rec(new(2026, 1, 19), 4, cars: 2, kwh: 10)); // відключення за графіком 4/4

        var r = Svc(s).CalculateHiddenWeek(ds, new HiddenWeekRequest { GeneratorKw = 20 });

        Assert.Equal(3, r.FuelLiters, 3);                 // 10 * 0.3
        Assert.Equal(195, r.FuelCostUah, 3);              // 3 * 65
        Assert.Equal(97.5, r.CostPerCarUah, 3);           // 195 / 2
        Assert.Equal(2 * 340 - 195, r.NetMarginUah, 3);   // 485
    }
}
