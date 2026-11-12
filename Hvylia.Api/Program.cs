using Hvylia.Api.Models;
using Hvylia.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddSingleton(builder.Configuration.GetSection("Calc").Get<CalcSettings>() ?? new CalcSettings());
builder.Services.AddSingleton<DataStore>();
builder.Services.AddSingleton<DataParserService>();
builder.Services.AddSingleton<DemoDataGenerator>();
builder.Services.AddSingleton<ICalculationService, CalculationService>();
builder.Services.AddSingleton<CsvExportService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapGet("/status", (DataStore store) =>
{
    var ds = store.Dataset;
    return new StatusResponse(ds is not null, ds?.Hourly.Count ?? 0, ds?.Equipment.Count ?? 0,
        ds?.Hourly.MinBy(r => r.Date)?.Date, ds?.Hourly.MaxBy(r => r.Date)?.Date);
});

// POST /api/upload — multipart (.csv/.xlsx) або ?demo=true
api.MapPost("/upload", async (HttpRequest req, DataStore store, DataParserService parser, DemoDataGenerator demo) =>
{
    if (req.Query["demo"] == "true")
    {
        store.SetDataset(demo.GenerateYear());
        return Results.Ok(new { source = "demo" });
    }
    if (!req.HasFormContentType) return Results.BadRequest("Очікується multipart/form-data");

    var file = (await req.ReadFormAsync()).Files.FirstOrDefault();
    if (file is null) return Results.BadRequest("Файл не передано");

    try
    {
        await using var stream = file.OpenReadStream();
        var ds = parser.Parse(stream, file.FileName);
        store.SetDataset(ds);
        return Results.Ok(new { source = file.FileName, rows = ds.Hourly.Count, equipment = ds.Equipment.Count });
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Не вдалося прочитати файл: {ex.Message}");
    }
}).DisableAntiforgery();

// POST /api/equipment — задати/замінити обладнання вручну (для CSV, де аркуша «Обладнання» немає)
api.MapPost("/equipment", (List<EquipmentItem> items, DataStore store) =>
{
    store.SetEquipment(items);
    return Results.Ok(new { equipment = items.Count });
});

// GET /api/baseline — показники №1, №2, №3
api.MapGet("/baseline", (DataStore store, ICalculationService calc) =>
    store.Dataset is { } ds ? Results.Ok(calc.CalculateBaseline(ds)) : Results.BadRequest("Спочатку /api/upload"));

// POST /api/simulate-hidden-week — показники №4, №5, №6 (тіло опційне)
api.MapPost("/simulate-hidden-week", (HiddenWeekRequest? body, DataStore store, ICalculationService calc) =>
{
    if (store.Dataset is not { } ds) return Results.BadRequest("Спочатку /api/upload");
    var req = body ?? new HiddenWeekRequest();
    store.SetHiddenWeekRequest(req);
    return Results.Ok(calc.CalculateHiddenWeek(ds, req));
});

// GET /api/export-csv?team=N
api.MapGet("/export-csv", (string? team, DataStore store, ICalculationService calc, CsvExportService exporter) =>
{
    if (store.Dataset is not { } ds) return Results.BadRequest("Спочатку /api/upload");
    var b = calc.CalculateBaseline(ds);
    var h = calc.CalculateHiddenWeek(ds, store.LastHiddenWeekRequest);
    var (bytes, name) = exporter.Build(b, h, team ?? "X");
    return Results.File(bytes, "text/csv; charset=utf-8", name);
});

app.Run();

public partial class Program { }
