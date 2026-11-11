using System.Collections.ObjectModel;
namespace TelecomFinance.Domain;

public sealed record MonthParameters(int Number, int ActiveClients, int AvgBillableMinutesPerClient,
    decimal PricePerMinute, decimal MonthlyFeePerClient, decimal VariableCostPerClient,
    decimal OperationalCosts, int PaymentPercent);

public sealed class FinancialPlan
{
    public Guid Id { get; }
    public string Name { get; }
    public decimal InitialCash { get; }
    public int StartYear { get; }
    public int StartMonth { get; }
    public ReadOnlyCollection<MonthParameters> Months { get; }

    public FinancialPlan(Guid id, string name, decimal initialCash, int startYear, int startMonth,
        IEnumerable<MonthParameters> months)
    {
        if (months is null) throw new FinanceValidationException("Months", "Укажите три месяца.");
        Id = id; Name = name?.Trim() ?? string.Empty; InitialCash = initialCash;
        StartYear = startYear; StartMonth = startMonth;
        Months = Array.AsReadOnly(months.ToArray());
    }
}

public sealed record MonthReport(int Number, decimal OpeningCash, decimal OpeningDebt,
    decimal Revenue, decimal VariableExpenses, decimal ContributionMargin,
    decimal TotalExpenses, decimal Profit, decimal CurrentPayment, decimal Receipts,
    decimal ClosingCash, decimal ClosingDebt, long BillableMinutes,
    decimal CallRevenue, decimal SubscriptionRevenue)
{
    public bool HasDeficit => ClosingCash < 0;
}

public sealed record FinancialSummary(decimal TotalProfit, decimal FinalCash,
    decimal FinalDebt, decimal RequiredFinancing);

public sealed class PlanForecast
{
    public Guid PlanId { get; }
    public string PlanName { get; }
    public DateOnly StartPeriod { get; }
    public ReadOnlyCollection<MonthReport> Months { get; }
    public FinancialSummary Summary { get; }
    public PlanForecast(Guid planId, string planName, DateOnly startPeriod,
        IEnumerable<MonthReport> months, FinancialSummary summary)
    {
        PlanId = planId; PlanName = planName; StartPeriod = startPeriod;
        Months = Array.AsReadOnly(months.ToArray()); Summary = summary;
    }
}

public sealed record ComparisonSide(Guid PlanId, string PlanName, FinancialSummary Summary);
public sealed record PlanComparison(ComparisonSide Left, ComparisonSide Right);

public sealed class FinanceValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
