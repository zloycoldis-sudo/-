# Хвиля — Backend (C# / .NET 8)

ASP.NET Core Minimal API: аналіз автомийки, підбір генератора (R4) та «прихований тиждень».

## Запуск
```bash
dotnet restore
dotnet test
cd Hvylia.Api && dotnet run     # Swagger: /swagger
```

## Структура
```
Hvylia.Api/
  Program.cs                      # ендпоінти + DI
  appsettings.json                # секція "Calc" (тариф, ряд потужностей, лінійка палива, ТО)
  Models/CarWashModels.cs
  Services/
    DataParserService.cs          # CSV (; , кома) + XLSX, санітарія нічної аномалії
    DemoDataGenerator.cs          # demo-рік
    CalculationService.cs         # №1–№6
    CsvExportService.cs           # team_<N>_відповідь.csv (UTF-8 BOM)
    DataStore.cs                  # in-memory стан
  wwwroot/                        # index.html, app.js (заглушка)
Hvylia.Tests/                     # xUnit
```

## API
| Метод | Шлях | Показники |
|---|---|---|
| GET  | /api/status | — |
| POST | /api/upload (`file` або `?demo=true`) | — |
| POST | /api/equipment | задати обладнання вручну (CSV без аркуша «Обладнання») |
| GET  | /api/baseline | №1, №2, №3 |
| POST | /api/simulate-hidden-week | №4, №5, №6 |
| GET  | /api/export-csv?team=N | усі шість |

## Припущення, які треба звірити з умовою кейсу
1. Поріг санітарії: `works==0 && value > 10` → `/1000` (Gemini: 10, docx: 50).
2. Тариф розподілу/постачання до ціни РДН (`DistributionTariffUahPerKwh`).
3. Правило R4 (пускові струми, одночасність, ряд типорозмірів).
4. Лінійка палива генератора, ТО (`appsettings.json`) — зараз плейсхолдери.
5. «4 через 4»: з чого починається доба (`FirstBlockIsOutage`).
6. Чи працює генератор вночі при T<0 (підігрів труб) та обмеження авто пропускною здатністю.
7. Формат і назви рядків у `team_X_відповідь.csv`.
8. Заголовки аркуша «Обладнання» (зараз пошук за підрядками: назва / потужн / пуск / кільк).

## Docker (для фронтендера)
```bash
docker compose up --build
```
- API: http://localhost:5000/api/status
- Swagger: http://localhost:5000/swagger
- Зупинити: `docker compose down`

Без compose:
```bash
docker build -t hvylia-api .
docker run --rm -p 5000:8080 hvylia-api
```
Якщо в `.csproj` стоїть `net10.0`, збирайте з `--build-arg DOTNET_VERSION=10.0`
(за замовчуванням Dockerfile підставляє 8.0 сам).
