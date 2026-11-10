using System.Globalization;
using System.Text.Json;
using TaxiFinance.Application;
using TaxiFinance.Domain;
using Xunit;
namespace TaxiFinance.Tests;

public sealed class ForecastTests
{
    private readonly ForecastService forecasts = new(new FinancialCalculator());

    [Theory]
    [InlineData("A0")]
    [InlineData("A50")]
    [InlineData("A100")]
    [InlineData("no_trips")]
    [InlineData("zero_cash")]
    [InlineData("updated_B")]
    [InlineData("kopeck")]
    public void AcceptanceExamples_MatchLab1ExpectedFile(string scenario)
    {
        var basis = TestPlans.Standard();
        var plan = scenario switch
        {
            "A0" => TestPlans.Standard(0),
            "A50" => basis,
            "A100" => TestPlans.Standard(100),
            "no_trips" => TestPlans.WithMonths(basis.Months.Select(m => m with { Quantity = 0 })),
            "zero_cash" => new FinancialPlan(TestPlans.A, "Нулевые деньги", 75000m,
                basis.Months.Select(m => m with { FixedCost = 100000m })),
            "updated_B" => TestPlans.WithMonths(basis.Months.Select(m => m.Number == 1 ? m with { PaymentPercent = 100 } : m)),
            "kopeck" => new FinancialPlan(TestPlans.A, "Копейка", 0,
                Enumerable.Range(1, 3).Select(n => new MonthParameters(n, 1, 0.01m, 0, 0, 50))),
            _ => throw new InvalidOperationException()
        };
        var actual = forecasts.Forecast(plan);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "lab1-expected.json")));
        var expected = json.RootElement.GetProperty(scenario);
        var expectedMonths = expected.GetProperty("months").EnumerateArray().ToArray();
        Assert.Equal(3, actual.Months.Count);
        for (int i = 0; i < 3; i++)
        {
            var row = actual.Months[i]; var e = expectedMonths[i];
            Assert.Equal(i + 1, row.Number);
            Assert.Equal(N(e, "revenue"), row.Revenue);
            Assert.Equal(N(e, "variable"), row.VariableExpenses);
            Assert.Equal(N(e, "margin"), row.ContributionMargin);
            Assert.Equal(N(e, "expenses"), row.TotalExpenses);
            Assert.Equal(N(e, "profit"), row.Profit);
            Assert.Equal(N(e, "current_payment"), row.CurrentPayment);
            Assert.Equal(N(e, "income"), row.Receipts);
            Assert.Equal(N(e, "cash"), row.ClosingCash);
            Assert.Equal(N(e, "debt"), row.ClosingDebt);
            Assert.Equal(i == 0 ? plan.InitialCash : N(expectedMonths[i - 1], "cash"), row.OpeningCash);
            Assert.Equal(i == 0 ? 0m : N(expectedMonths[i - 1], "debt"), row.OpeningDebt);
        }
        Assert.Equal(N(expected, "profit"), actual.Summary.TotalProfit);
        Assert.Equal(N(expected, "cash"), actual.Summary.FinalCash);
        Assert.Equal(N(expected, "debt"), actual.Summary.FinalDebt);
        Assert.Equal(N(expected, "financing"), actual.Summary.RequiredFinancing);
    }
    private static decimal N(JsonElement element, string key) => decimal.Parse(element.GetProperty(key).GetString()!, CultureInfo.InvariantCulture);

    [Fact]
    public void VariedMonths_CarryCashAndDebt_WithLiteralExpectedRows()
    {
        var plan = new FinancialPlan(TestPlans.A, "Разные месяцы", 10000m, new[] {
            new MonthParameters(1, 100, 300, 100, 5000, 50),
            new MonthParameters(2, 50, 400, 120, 6000, 0),
            new MonthParameters(3, 80, 250, 90, 7000, 100)
        });
        var result = forecasts.Forecast(plan);
        Assert.Equal(new MonthReport(1, 10000, 0, 30000, 10000, 20000, 15000, 15000, 15000, 15000, 10000, 15000), result.Months[0]);
        Assert.Equal(new MonthReport(2, 10000, 15000, 20000, 6000, 14000, 12000, 8000, 0, 15000, 13000, 20000), result.Months[1]);
        Assert.Equal(new MonthReport(3, 13000, 20000, 20000, 7200, 12800, 14200, 5800, 20000, 40000, 38800, 0), result.Months[2]);
        Assert.Equal(new FinancialSummary(28800, 38800, 0, 0), result.Summary);
    }

    [Fact]
    public void NegativeCash_ContinuesAndFundingIsMaximum_NotSum()
    {
        var result = forecasts.Forecast(TestPlans.Standard(0));
        Assert.Equal(new[] { -70000m, -40000m, -10000m }, result.Months.Select(m => m.ClosingCash));
        Assert.All(result.Months, m => Assert.True(m.HasDeficit));
        Assert.Equal(70000m, result.Summary.RequiredFinancing);
        Assert.NotEqual(120000m, result.Summary.RequiredFinancing);
        Assert.Equal(-10000m, result.Summary.FinalCash);
        Assert.Equal(150000m, result.Summary.FinalDebt);
    }

    [Fact]
    public void ZeroCash_IsNotDeficit()
    {
        var basis = TestPlans.Standard();
        var plan = new FinancialPlan(basis.Id, basis.Name, 75000m, basis.Months.Select(m => m with { FixedCost = 100000m }));
        var result = forecasts.Forecast(plan);
        Assert.All(result.Months, m => { Assert.Equal(0m, m.ClosingCash); Assert.False(m.HasDeficit); });
        Assert.Equal(0m, result.Summary.RequiredFinancing);
    }

    [Fact]
    public void PriceBelowVariableCost_IsValidLoss()
    {
        var plan = new FinancialPlan(TestPlans.A, "Убыточный", 100m,
            Enumerable.Range(1, 3).Select(n => new MonthParameters(n, 2, 10, 20, 5, 100)));
        var result = forecasts.Forecast(plan);
        Assert.All(result.Months, m => { Assert.Equal(-20m, m.ContributionMargin); Assert.Equal(-25m, m.Profit); });
        Assert.Equal(new FinancialSummary(-75, 25, 0, 0), result.Summary);
    }

    [Fact]
    public void AllZeros_IsValid_NoFunding()
    {
        var plan = new FinancialPlan(TestPlans.A, "Нулевой", 0m,
            Enumerable.Range(1, 3).Select(n => new MonthParameters(n, 0, 0, 0, 0, 0)));
        Assert.Equal(new FinancialSummary(0, 0, 0, 0), forecasts.Forecast(plan).Summary);
    }

    [Fact]
    public void RepeatedForecast_StartsFromInputs_AndDoesNotMutate()
    {
        var plan = TestPlans.Standard(0);
        var original = plan.Months.ToArray();
        var first = forecasts.Forecast(plan); var second = forecasts.Forecast(plan);
        Assert.Equal(first.Months.ToArray(), second.Months.ToArray());
        Assert.Equal(first.Summary, second.Summary);
        Assert.Equal(50000m, second.Months[0].OpeningCash);
        Assert.Equal(0m, second.Months[0].OpeningDebt);
        Assert.Equal(original, plan.Months.ToArray());
        Assert.Equal(50000m, plan.InitialCash);
    }

    [Fact]
    public void EditingOneMonth_RecalculatesWithoutChangingOldPlanOrResult()
    {
        var original = TestPlans.Standard();
        var before = forecasts.Forecast(original);
        var edited = new FinancialPlan(original.Id, original.Name, original.InitialCash,
            original.Months.Select(m => m.Number == 1 ? m with { PaymentPercent = 100 } : m));
        var after = forecasts.Forecast(edited);
        Assert.Equal(new[] { 80000m, 35000m, 65000m }, after.Months.Select(m => m.ClosingCash));
        Assert.Equal(new[] { 5000m, 35000m, 65000m }, before.Months.Select(m => m.ClosingCash));
        Assert.Equal(50, original.Months[0].PaymentPercent);
        Assert.Equal(100, edited.Months[0].PaymentPercent);
        Assert.Equal(after.Months.ToArray(), forecasts.Forecast(edited).Months.ToArray());
    }

    [Fact]
    public void UnsortedMonths_AreCalculatedInOrder_WithoutSortingInputInPlace()
    {
        var plan = TestPlans.WithMonths(new[] { TestPlans.Month(3), TestPlans.Month(1), TestPlans.Month(2) });
        var result = forecasts.Forecast(plan);
        Assert.Equal(new[] { 1, 2, 3 }, result.Months.Select(m => m.Number));
        Assert.Equal(new[] { 3, 1, 2 }, plan.Months.Select(m => m.Number));
        Assert.Equal(65000m, result.Summary.FinalCash);
    }

    [Fact]
    public void InputAndResultCollections_CannotBeMutated()
    {
        var array = new[] { TestPlans.Month(1), TestPlans.Month(2), TestPlans.Month(3) };
        var plan = TestPlans.WithMonths(array);
        array[0] = array[0] with { Quantity = 0 };
        Assert.Equal(500, plan.Months[0].Quantity);
        Assert.Throws<NotSupportedException>(() => ((IList<MonthParameters>)plan.Months).Clear());
        var result = forecasts.Forecast(plan);
        Assert.Throws<NotSupportedException>(() => ((IList<MonthReport>)result.Months).Clear());
    }

    [Theory]
    [InlineData("0.01", "0.01", "0.00")]
    [InlineData("0.05", "0.03", "0.02")]
    [InlineData("0.03", "0.02", "0.01")]
    public void Payment_RoundsAwayFromZero_DebtIsRevenueMinusPayment(string price, string payment, string debt)
    {
        decimal P(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);
        var row = new FinancialCalculator().CalculateMonth(new(1, 1, P(price), 0, 0, 50), 0, 0);
        Assert.Equal(P(payment), row.CurrentPayment);
        Assert.Equal(P(debt), row.ClosingDebt);
        Assert.Equal(P(price), row.CurrentPayment + row.ClosingDebt);
    }

    [Fact]
    public void MonthlyCore_AllowsNegativeOpeningCash_ForCarry()
    {
        var row = new FinancialCalculator().CalculateMonth(TestPlans.Month(2, 0), -70000m, 150000m);
        Assert.Equal(-40000m, row.ClosingCash);
        Assert.Equal(150000m, row.Receipts);
    }
}
