using TaxiFinance.Domain;
namespace TaxiFinance.Application;

public sealed class ForecastService(FinancialCalculator calculator)
{
    public PlanForecast Forecast(FinancialPlan plan)
    {
        PlanValidator.Validate(plan);
        decimal cash = plan.InitialCash;
        decimal debt = 0;
        var reports = new List<MonthReport>(3);
        foreach (var month in plan.Months.OrderBy(m => m.Number))
        {
            var report = calculator.CalculateMonth(month, cash, debt);
            reports.Add(report);
            cash = report.ClosingCash;
            debt = report.ClosingDebt;
        }
        return new(plan.Id, plan.Name, reports, calculator.Summarize(reports));
    }
}
