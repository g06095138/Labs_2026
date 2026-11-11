namespace TelecomFinance.Domain;

public sealed class FinancialCalculator
{
    public MonthReport CalculateMonth(MonthParameters month, decimal openingCash, decimal openingDebt)
    {
        PlanValidator.ValidateMonth(month);
        if (decimal.Round(openingCash, 2) != openingCash)
            throw new FinanceValidationException("OpeningCash", "Начальный остаток должен быть выражен в копейках.");
        if (openingDebt < 0 || decimal.Round(openingDebt, 2) != openingDebt)
            throw new FinanceValidationException("OpeningDebt", "Начальный долг неотрицателен и выражен в копейках.");
        try
        {
            long billableMinutes = (long)month.ActiveClients * month.AvgBillableMinutesPerClient;
            decimal callRevenue = billableMinutes * month.PricePerMinute;
            decimal subscriptionRevenue = month.ActiveClients * month.MonthlyFeePerClient;
            decimal revenue = callRevenue + subscriptionRevenue;
            decimal variableExpenses = month.ActiveClients * month.VariableCostPerClient;
            decimal margin = revenue - variableExpenses;
            decimal expenses = variableExpenses + month.OperationalCosts;
            decimal profit = revenue - expenses;
            decimal payment = decimal.Round(revenue * month.PaymentPercent / 100m,
                2, MidpointRounding.AwayFromZero);
            decimal debt = revenue - payment;
            decimal receipts = payment + openingDebt;
            decimal cash = openingCash + receipts - expenses;
            return new(month.Number, openingCash, openingDebt, revenue, variableExpenses,
                margin, expenses, profit, payment, receipts, cash, debt,
                billableMinutes, callRevenue, subscriptionRevenue);
        }
        catch (OverflowException)
        {
            throw new FinanceValidationException($"Months[{month.Number}].Calculation", "Результат превышает диапазон decimal.");
        }
    }

    public FinancialSummary Summarize(IReadOnlyList<MonthReport> months)
    {
        if (months is null || months.Count != 3 || months.Any(m => m is null)
            || !months.Select(m => m.Number).SequenceEqual(new[] { 1, 2, 3 }))
            throw new FinanceValidationException("Reports", "Для итогов нужны три отчёта в порядке 1, 2, 3.");
        try
        {
            return new(months.Sum(m => m.Profit), months[2].ClosingCash, months[2].ClosingDebt,
                Math.Max(0m, -months.Min(m => m.ClosingCash)));
        }
        catch (OverflowException)
        {
            throw new FinanceValidationException("Summary", "Итог превышает диапазон decimal.");
        }
    }
}
