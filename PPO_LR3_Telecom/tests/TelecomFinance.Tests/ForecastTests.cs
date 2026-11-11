using System.Globalization;
using TelecomFinance.Application;
using TelecomFinance.Domain;
using Xunit;

namespace TelecomFinance.Tests;

public sealed class ForecastTests
{
    private readonly ForecastService forecasts = new(new FinancialCalculator());

    [Theory]
    [InlineData(0, -70000, -40000, -10000, 150000, 70000)]
    [InlineData(50, 5000, 35000, 65000, 75000, 0)]
    [InlineData(100, 80000, 110000, 140000, 0, 0)]
    public void AcceptancePaymentPlans_MatchManualMonthlyAndFinalValues(
        int percent, int cash1, int cash2, int cash3, int finalDebt, int funding)
    {
        var result = forecasts.Forecast(TestPlans.Standard(percent));
        decimal[] expectedCash = [cash1, cash2, cash3];
        decimal payment = percent switch { 0 => 0m, 50 => 75000m, _ => 150000m };
        decimal debt = percent switch { 0 => 150000m, 50 => 75000m, _ => 0m };
        Assert.Equal(3, result.Months.Count);
        for (int i = 0; i < 3; i++)
        {
            var row = result.Months[i];
            Assert.Equal(i + 1, row.Number);
            Assert.Equal(20000L, row.BillableMinutes);
            Assert.Equal(100000m, row.CallRevenue);
            Assert.Equal(50000m, row.SubscriptionRevenue);
            Assert.Equal(150000m, row.Revenue);
            Assert.Equal(50000m, row.VariableExpenses);
            Assert.Equal(100000m, row.ContributionMargin);
            Assert.Equal(120000m, row.TotalExpenses);
            Assert.Equal(30000m, row.Profit);
            Assert.Equal(payment, row.CurrentPayment);
            Assert.Equal(i == 0 ? payment : 150000m, row.Receipts);
            Assert.Equal(i == 0 ? 50000m : expectedCash[i - 1], row.OpeningCash);
            Assert.Equal(i == 0 ? 0m : debt, row.OpeningDebt);
            Assert.Equal(expectedCash[i], row.ClosingCash);
            Assert.Equal(debt, row.ClosingDebt);
        }
        Assert.Equal(new FinancialSummary(90000, cash3, finalDebt, funding), result.Summary);
    }

    [Fact]
    public void NoClients_MeansNoCallsFeesOrVariableExpenses_ButFixedCostsRemain()
    {
        var plan = TestPlans.WithMonths(Enumerable.Range(1, 3)
            .Select(n => TestPlans.Month(n) with { ActiveClients = 0 }));
        var result = forecasts.Forecast(plan);
        Assert.All(result.Months, row =>
        {
            Assert.Equal(0L, row.BillableMinutes);
            Assert.Equal(0m, row.CallRevenue);
            Assert.Equal(0m, row.SubscriptionRevenue);
            Assert.Equal(0m, row.Revenue);
            Assert.Equal(0m, row.VariableExpenses);
            Assert.Equal(0m, row.ContributionMargin);
            Assert.Equal(70000m, row.TotalExpenses);
            Assert.Equal(-70000m, row.Profit);
            Assert.Equal(0m, row.CurrentPayment);
            Assert.Equal(0m, row.Receipts);
            Assert.Equal(0m, row.ClosingDebt);
        });
        Assert.Equal(new[] { -20000m, -90000m, -160000m }, result.Months.Select(m => m.ClosingCash));
        Assert.Equal(new FinancialSummary(-210000, -160000, 0, 160000), result.Summary);
    }

    [Fact]
    public void ZeroMinutes_StillChargesSubscriptionAndServicingCosts()
    {
        var month = new MonthParameters(1, 10, 0, 5, 200, 50, 100, 100);
        var row = new FinancialCalculator().CalculateMonth(month, 1000, 0);
        Assert.Equal(0L, row.BillableMinutes);
        Assert.Equal(0m, row.CallRevenue);
        Assert.Equal(2000m, row.SubscriptionRevenue);
        Assert.Equal(2000m, row.Revenue);
        Assert.Equal(500m, row.VariableExpenses);
        Assert.Equal(1500m, row.ContributionMargin);
        Assert.Equal(600m, row.TotalExpenses);
        Assert.Equal(1400m, row.Profit);
        Assert.Equal(2400m, row.ClosingCash);
    }

    [Fact]
    public void ZeroClosingCash_IsValidAndNotDeficit()
    {
        var plan = new FinancialPlan(TestPlans.A, "Нулевой остаток", 75000m, 2026, 1,
            Enumerable.Range(1, 3).Select(n => TestPlans.Month(n) with { OperationalCosts = 100000m }));
        var result = forecasts.Forecast(plan);
        Assert.All(result.Months, row =>
        {
            Assert.Equal(0m, row.ClosingCash);
            Assert.Equal(0m, row.Profit);
            Assert.Equal(75000m, row.ClosingDebt);
            Assert.False(row.HasDeficit);
        });
        Assert.Equal(new FinancialSummary(0, 0, 75000, 0), result.Summary);
    }

    [Fact]
    public void VariedMonths_CarryDebtAndCash_UsingLiteralExpectedRows()
    {
        var plan = new FinancialPlan(TestPlans.A, "Разные месяцы", 10000m, 2026, 1, new[]
        {
            new MonthParameters(1, 10, 100, 20, 1000, 1000, 5000, 50),
            new MonthParameters(2, 5, 100, 30, 1000, 1200, 6000, 0),
            new MonthParameters(3, 8, 100, 20, 500, 900, 7000, 100)
        });
        var result = forecasts.Forecast(plan);
        Assert.Equal(new MonthReport(1, 10000, 0, 30000, 10000, 20000, 15000, 15000, 15000, 15000,
            10000, 15000, 1000, 20000, 10000), result.Months[0]);
        Assert.Equal(new MonthReport(2, 10000, 15000, 20000, 6000, 14000, 12000, 8000, 0, 15000,
            13000, 20000, 500, 15000, 5000), result.Months[1]);
        Assert.Equal(new MonthReport(3, 13000, 20000, 20000, 7200, 12800, 14200, 5800, 20000, 40000,
            38800, 0, 800, 16000, 4000), result.Months[2]);
        Assert.Equal(new FinancialSummary(28800, 38800, 0, 0), result.Summary);
    }

    [Fact]
    public void NegativeCash_ContinuesThreeMonths_AndFundingIsWorstDeficit()
    {
        var result = forecasts.Forecast(TestPlans.Standard(0));
        Assert.Equal(new[] { -70000m, -40000m, -10000m }, result.Months.Select(m => m.ClosingCash));
        Assert.All(result.Months, row => Assert.True(row.HasDeficit));
        Assert.Equal(70000m, result.Summary.RequiredFinancing);
        Assert.NotEqual(120000m, result.Summary.RequiredFinancing);
        Assert.Equal(-10000m, result.Summary.FinalCash);
        Assert.Equal(150000m, result.Summary.FinalDebt);
    }

    [Fact]
    public void RepeatedForecast_StartsFromOriginalCashAndZeroDebt_WithoutMutation()
    {
        var plan = TestPlans.Standard(0);
        var inputs = plan.Months.ToArray();
        var first = forecasts.Forecast(plan);
        var second = forecasts.Forecast(plan);
        Assert.Equal(first.Months.ToArray(), second.Months.ToArray());
        Assert.Equal(first.Summary, second.Summary);
        Assert.Equal(50000m, second.Months[0].OpeningCash);
        Assert.Equal(0m, second.Months[0].OpeningDebt);
        Assert.Equal(inputs, plan.Months.ToArray());
        Assert.Equal(50000m, plan.InitialCash);
    }

    [Fact]
    public void EditedFirstMonth_IsRecalculated_WithoutChangingEarlierPlanOrResult()
    {
        var original = TestPlans.Standard();
        var before = forecasts.Forecast(original);
        var edited = new FinancialPlan(original.Id, original.Name, original.InitialCash,
            original.StartYear, original.StartMonth,
            original.Months.Select(m => m.Number == 1 ? m with { PaymentPercent = 100 } : m));
        var after = forecasts.Forecast(edited);
        Assert.Equal(new[] { 80000m, 35000m, 65000m }, after.Months.Select(m => m.ClosingCash));
        Assert.Equal(new[] { 5000m, 35000m, 65000m }, before.Months.Select(m => m.ClosingCash));
        Assert.Equal(50, original.Months[0].PaymentPercent);
        Assert.Equal(100, edited.Months[0].PaymentPercent);
        Assert.Equal(after.Months.ToArray(), forecasts.Forecast(edited).Months.ToArray());
    }

    [Fact]
    public void UnsortedMonths_AreCalculatedInOrder_WithoutChangingTheirInputOrder()
    {
        var plan = TestPlans.WithMonths(new[] { TestPlans.Month(3), TestPlans.Month(1), TestPlans.Month(2) });
        var result = forecasts.Forecast(plan);
        Assert.Equal(new[] { 1, 2, 3 }, result.Months.Select(m => m.Number));
        Assert.Equal(new[] { 3, 1, 2 }, plan.Months.Select(m => m.Number));
        Assert.Equal(65000m, result.Summary.FinalCash);
    }

    [Fact]
    public void InputArrayAndReadOnlyCollections_CannotChangeThePlanOrForecast()
    {
        var months = new[] { TestPlans.Month(1), TestPlans.Month(2), TestPlans.Month(3) };
        var plan = TestPlans.WithMonths(months);
        months[0] = months[0] with { ActiveClients = 0 };
        Assert.Equal(100, plan.Months[0].ActiveClients);
        Assert.Throws<NotSupportedException>(() => ((IList<MonthParameters>)plan.Months).Clear());
        var result = forecasts.Forecast(plan);
        Assert.Throws<NotSupportedException>(() => ((IList<MonthReport>)result.Months).Clear());
    }

    [Theory]
    [InlineData("0.01", "0.01", "0.00")]
    [InlineData("0.03", "0.02", "0.01")]
    [InlineData("0.05", "0.03", "0.02")]
    public void PaymentRoundsAwayFromZero_AndDebtPreservesEveryKopeck(string rate, string paid, string unpaid)
    {
        decimal Parse(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);
        var month = new MonthParameters(1, 1, 1, Parse(rate), 0, 0, 0, 50);
        var row = new FinancialCalculator().CalculateMonth(month, 0, 0);
        Assert.Equal(Parse(paid), row.CurrentPayment);
        Assert.Equal(Parse(unpaid), row.ClosingDebt);
        Assert.Equal(Parse(rate), row.CurrentPayment + row.ClosingDebt);
    }

    [Fact]
    public void ServicingCostAboveMonthlyBill_IsValidLoss()
    {
        var plan = new FinancialPlan(TestPlans.A, "Убыточный тариф", 100, 2026, 1,
            Enumerable.Range(1, 3).Select(n => new MonthParameters(n, 2, 1, 5, 5, 20, 5, 100)));
        var result = forecasts.Forecast(plan);
        Assert.All(result.Months, row =>
        {
            Assert.Equal(-20m, row.ContributionMargin);
            Assert.Equal(-25m, row.Profit);
        });
        Assert.Equal(new FinancialSummary(-75, 25, 0, 0), result.Summary);
    }

    [Fact]
    public void ZeroInputs_AreValidAndRequireNoFinancing()
    {
        var plan = new FinancialPlan(TestPlans.A, "Пустой рынок", 0, 2026, 1,
            Enumerable.Range(1, 3).Select(n => new MonthParameters(n, 0, 0, 0, 0, 0, 0, 0)));
        Assert.Equal(new FinancialSummary(0, 0, 0, 0), forecasts.Forecast(plan).Summary);
    }

    [Fact]
    public void ForecastCalendar_CrossesYearBoundary()
    {
        var result = forecasts.Forecast(TestPlans.Standard());
        Assert.Equal(new DateOnly(2026, 11, 1), result.StartPeriod);
        Assert.Equal(new DateOnly(2026, 12, 1), result.StartPeriod.AddMonths(1));
        Assert.Equal(new DateOnly(2027, 1, 1), result.StartPeriod.AddMonths(2));
        Assert.Equal(65000m, result.Summary.FinalCash);
    }

    [Fact]
    public void MonthlyCore_AllowsNegativeOpeningCashCarriedFromPreviousMonth()
    {
        var row = new FinancialCalculator().CalculateMonth(TestPlans.Month(2, 0), -70000, 150000);
        Assert.Equal(-40000m, row.ClosingCash);
        Assert.Equal(150000m, row.Receipts);
    }
}
