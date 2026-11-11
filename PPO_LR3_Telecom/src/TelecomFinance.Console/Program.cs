using System.Globalization;
using System.Text.Json;
using TelecomFinance.Application;
using TelecomFinance.Domain;
using TelecomFinance.Console;

var forecasts = new ForecastService(new FinancialCalculator());
var comparisons = new ComparisonService(forecasts);
var json = args.Contains("--json", StringComparer.Ordinal);
var command = args.Where(a => a != "--json").ToArray();
try
{
    if (command.Length == 0 || command is ["demo"])
    {
        if (json) throw new FinanceValidationException("Command", "Для JSON используйте forecast или compare.");
        foreach (int percent in new[] { 0, 50, 100 }) PrintForecast(forecasts.Forecast(Sample(percent)));
        PrintComparison(comparisons.Compare(Sample(0), Sample(50)));
    }
    else if (command is ["forecast", var path])
    {
        var result = forecasts.Forecast(PlanInput.Read(path));
        if (json) PrintJson(result); else PrintForecast(result);
    }
    else if (command is ["compare", var left, var right])
    {
        var result = comparisons.Compare(PlanInput.Read(left), PlanInput.Read(right));
        if (json) PrintJson(result); else PrintComparison(result);
    }
    else throw new FinanceValidationException("Command", "Команды: demo | forecast <план.json> [--json] | compare <левый.json> <правый.json> [--json]");
    return 0;
}
catch (FinanceValidationException ex)
{
    Error(ex.Field, ex.Message); return 2;
}
catch (JsonException ex)
{
    Error(ex.Path ?? "Input", "Некорректный JSON или тип поля: " + ex.Message); return 2;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Error("InputFile", "Не удалось прочитать файл плана: " + ex.Message); return 2;
}

void Error(string field, string message)
{
    if (json) PrintJson(new { Error = new { Field = field, Message = message } });
    else System.Console.Error.WriteLine($"Ошибка {field}: {message}");
}
static FinancialPlan Sample(int percent) => new(Guid.Parse($"00000000-0000-4000-8000-{percent:000000000000}"),
    $"Оператор связи — {percent}% сразу", 50000m, 2026, 11,
    Enumerable.Range(1, 3).Select(n => new MonthParameters(n, 100, 200, 5m, 500m, 500m, 70000m, percent)));
static void PrintJson(object value) => System.Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
static string M(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);
static void PrintForecast(PlanForecast result)
{
    System.Console.WriteLine($"\n{result.PlanName} ({result.PlanId})");
    System.Console.WriteLine("Месяц | Минуты | Выручка звонков | Абонплата | Выручка | Марж. доход | Расходы | Прибыль | Оплата | Поступления | Деньги | Долг");
    foreach (var m in result.Months)
    {
        var period = result.StartPeriod.AddMonths(m.Number - 1).ToString("yyyy-MM", CultureInfo.InvariantCulture);
        System.Console.WriteLine($"{m.Number} ({period}) | {m.BillableMinutes} | {M(m.CallRevenue)} | {M(m.SubscriptionRevenue)} | {M(m.Revenue)} | {M(m.ContributionMargin)} | {M(m.TotalExpenses)} | {M(m.Profit)} | {M(m.CurrentPayment)} | {M(m.Receipts)} | {M(m.ClosingCash)} | {M(m.ClosingDebt)}{(m.HasDeficit ? " — Не хватает денег" : "")}");
    }
    System.Console.WriteLine($"Итого: прибыль {M(result.Summary.TotalProfit)}; деньги {M(result.Summary.FinalCash)}; долг {M(result.Summary.FinalDebt)}; финансирование {M(result.Summary.RequiredFinancing)} RUB");
}
static void PrintComparison(PlanComparison result)
{
    System.Console.WriteLine($"\nСравнение: {result.Left.PlanName} | {result.Right.PlanName}");
    System.Console.WriteLine($"Прибыль: {M(result.Left.Summary.TotalProfit)} | {M(result.Right.Summary.TotalProfit)}");
    System.Console.WriteLine($"Деньги: {M(result.Left.Summary.FinalCash)} | {M(result.Right.Summary.FinalCash)}");
    System.Console.WriteLine($"Долг: {M(result.Left.Summary.FinalDebt)} | {M(result.Right.Summary.FinalDebt)}");
    System.Console.WriteLine($"Финансирование: {M(result.Left.Summary.RequiredFinancing)} | {M(result.Right.Summary.RequiredFinancing)}");
}
