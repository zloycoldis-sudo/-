using Hvylia.Api.Models;

namespace Hvylia.Api.Services;

/// <summary>DEMO-дані року 01.11.2025–31.10.2026 для швидкого старту. Усі числа умовні.</summary>
public sealed class DemoDataGenerator
{
    public Dataset GenerateYear(int seed = 42)
    {
        var rnd = new Random(seed);
        var ds = new Dataset
        {
            Equipment = new()
            {
                new() { Name = "АВД (demo)",       RatedPowerKw = 3.0, StartingFactor = 3, Quantity = 3 },
                new() { Name = "Компресор (demo)", RatedPowerKw = 4.0, StartingFactor = 3, Quantity = 1 },
                new() { Name = "Пилосос (demo)",   RatedPowerKw = 1.2, StartingFactor = 2, Quantity = 3 },
                new() { Name = "Обігрів (demo)",   RatedPowerKw = 2.4, StartingFactor = 1, Quantity = 1 },
            }
        };

        var start = new DateTime(2025, 11, 1);
        var end = new DateTime(2026, 10, 31, 23, 0, 0);
        for (var t = start; t <= end; t = t.AddHours(1))
        {
            var works = t.Hour is >= 8 and < 20 ? 1 : 0;
            var dayOfYear = (t - start).Days;
            var temp = 8 + 15 * Math.Sin((t.DayOfYear - 110) / 365.0 * 2 * Math.PI) - 5 + rnd.NextDouble() * 4;
            var demand = works == 1 ? rnd.Next(0, 6) : 0;
            var outage = dayOfYear % 4 == 0 && t.Hour is >= 12 and < 16;

            ds.Hourly.Add(new HourlyRecord
            {
                Date = t.Date,
                DayOfWeek = t.DayOfWeek.ToString(),
                Hour = t.Hour,
                Works = works,
                DemandCars = demand,
                ConsumptionKwh = works == 1 ? 2 + demand * 2.5 : (temp < 0 ? 2.4 : 0.4),
                GridAvailable = outage ? 0 : 1,
                RdnPrice = 3000 + (decimal)(rnd.NextDouble() * 2000),
                Temperature = Math.Round(temp, 1)
            });
        }
        return ds;
    }
}
