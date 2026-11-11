using System.Globalization;
using TelecomFinance.Application;
using TelecomFinance.Domain;
using Xunit;

namespace TelecomFinance.Tests;

public sealed class ValidationTests
{
    private readonly ForecastService forecasts = new(new FinancialCalculator());

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidPaymentPercent_IsRejectedWithFieldAndReason(int percent)
    {
        var plan = TestPlans.WithMonths(Enumerable.Range(1, 3)
            .Select(n => TestPlans.Month(n) with { PaymentPercent = percent }));
        var error = Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(plan));
        Assert.Equal("Months[1].PaymentPercent", error.Field);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void EmptyOrWhitespaceName_IsRejected(string name)
    {
        var plan = new FinancialPlan(TestPlans.A, name, 50000, 2026, 11, TestPlans.Standard().Months);
        var error = Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(plan));
        Assert.Equal("Name", error.Field);
    }

    [Fact]
    public void NameIsTrimmed_AndValidNameRemainsAvailableInForecast()
    {
        var plan = new FinancialPlan(TestPlans.A, "  Оператор связи  ", 50000, 2026, 11, TestPlans.Standard().Months);
        Assert.Equal("Оператор связи", plan.Name);
        Assert.Equal("Оператор связи", forecasts.Forecast(plan).PlanName);
    }

    [Fact]
    public void NameLongerThan200Characters_IsRejected()
    {
        var plan = new FinancialPlan(TestPlans.A, new string('а', 201), 50000, 2026, 11, TestPlans.Standard().Months);
        Assert.Equal("Name", Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(plan)).Field);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("0.001")]
    [InlineData("1000000000000.01")]
    public void InvalidInitialCash_IsRejected(string value)
    {
        var plan = new FinancialPlan(TestPlans.A, "Ошибка", decimal.Parse(value, CultureInfo.InvariantCulture),
            2026, 11, TestPlans.Standard().Months);
        Assert.Equal("InitialCash", Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(plan)).Field);
    }

    [Fact]
    public void EmptyIdentifier_IsRejected()
    {
        Assert.Equal("Id", Assert.Throws<FinanceValidationException>(
            () => forecasts.Forecast(TestPlans.Standard(id: Guid.Empty))).Field);
    }

    [Theory]
    [InlineData("PricePerMinute", "-0.01")]
    [InlineData("PricePerMinute", "0.001")]
    [InlineData("PricePerMinute", "1000000000000.01")]
    [InlineData("MonthlyFeePerClient", "-0.01")]
    [InlineData("MonthlyFeePerClient", "0.001")]
    [InlineData("MonthlyFeePerClient", "1000000000000.01")]
    [InlineData("VariableCostPerClient", "-0.01")]
    [InlineData("VariableCostPerClient", "0.001")]
    [InlineData("VariableCostPerClient", "1000000000000.01")]
    [InlineData("OperationalCosts", "-0.01")]
    [InlineData("OperationalCosts", "0.001")]
    [InlineData("OperationalCosts", "1000000000000.01")]
    public void InvalidMonthMoney_IsRejectedWithSpecificField(string field, string value)
    {
        decimal money = decimal.Parse(value, CultureInfo.InvariantCulture);
        var month = field switch
        {
            "PricePerMinute" => TestPlans.Month() with { PricePerMinute = money },
            "MonthlyFeePerClient" => TestPlans.Month() with { MonthlyFeePerClient = money },
            "VariableCostPerClient" => TestPlans.Month() with { VariableCostPerClient = money },
            "OperationalCosts" => TestPlans.Month() with { OperationalCosts = money },
            _ => throw new InvalidOperationException()
        };
        var error = Assert.Throws<FinanceValidationException>(() => PlanValidator.ValidateMonth(month));
        Assert.Equal("Months[1]." + field, error.Field);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000001)]
    public void InvalidClientCount_IsRejected(int clients)
    {
        var month = TestPlans.Month() with { ActiveClients = clients };
        Assert.Equal("Months[1].ActiveClients", Assert.Throws<FinanceValidationException>(
            () => PlanValidator.ValidateMonth(month)).Field);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000001)]
    public void InvalidBillableMinutes_IsRejected(int minutes)
    {
        var month = TestPlans.Month() with { AvgBillableMinutesPerClient = minutes };
        Assert.Equal("Months[1].AvgBillableMinutesPerClient", Assert.Throws<FinanceValidationException>(
            () => PlanValidator.ValidateMonth(month)).Field);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void MonthCountOtherThanThree_IsRejected(int count)
    {
        var plan = TestPlans.WithMonths(Enumerable.Range(1, count).Select(n => TestPlans.Month(n)));
        Assert.Equal("Months", Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(plan)).Field);
    }

    [Theory]
    [InlineData(1, 1, 3)]
    [InlineData(1, 2, 2)]
    [InlineData(0, 2, 3)]
    [InlineData(1, 2, 4)]
    public void DuplicateMissingOrOutOfRangeMonthNumbers_AreRejected(int first, int second, int third)
    {
        var plan = TestPlans.WithMonths(new[]
            { TestPlans.Month(first), TestPlans.Month(second), TestPlans.Month(third) });
        Assert.Equal("Months.Number", Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(plan)).Field);
    }

    [Fact]
    public void InvalidThirdMonth_IsRejected_WithoutChangingAnyInput()
    {
        var plan = TestPlans.WithMonths(new[]
            { TestPlans.Month(1), TestPlans.Month(2), TestPlans.Month(3) with { PaymentPercent = 101 } });
        var before = plan.Months.ToArray();
        var error = Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(plan));
        Assert.Equal("Months[3].PaymentPercent", error.Field);
        Assert.Equal(before, plan.Months.ToArray());
        Assert.Equal(50000m, plan.InitialCash);
    }

    [Theory]
    [InlineData(2019, 1, "StartYear")]
    [InlineData(2100, 1, "StartYear")]
    [InlineData(2026, 0, "StartMonth")]
    [InlineData(2026, 13, "StartMonth")]
    [InlineData(2099, 11, "StartYear")]
    [InlineData(2099, 12, "StartYear")]
    public void InvalidCalendarOrPlanExtendingBeyond2099_IsRejected(int year, int month, string field)
    {
        var plan = new FinancialPlan(TestPlans.A, "Ошибка периода", 50000, year, month, TestPlans.Standard().Months);
        Assert.Equal(field, Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(plan)).Field);
    }

    [Theory]
    [InlineData(2020, 1)]
    [InlineData(2099, 10)]
    public void CalendarBoundaryPlans_AreAccepted(int year, int month)
    {
        var plan = new FinancialPlan(TestPlans.A, "Граница периода", 50000, year, month, TestPlans.Standard().Months);
        Assert.Equal(new DateOnly(year, month, 1), forecasts.Forecast(plan).StartPeriod);
    }

    [Fact]
    public void NullPlanOrMonths_AreRejectedWithStructuredErrors()
    {
        Assert.Equal("Plan", Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(null!)).Field);
        Assert.Equal("Months", Assert.Throws<FinanceValidationException>(() =>
            new FinancialPlan(TestPlans.A, "Ошибка", 0, 2026, 1, null!)).Field);
        var plan = TestPlans.WithMonths(new[] { TestPlans.Month(1), null!, TestPlans.Month(3) });
        Assert.Equal("Months", Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(plan)).Field);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("0.001")]
    public void InvalidOpeningDebt_IsRejected(string value)
    {
        var error = Assert.Throws<FinanceValidationException>(() => new FinancialCalculator().CalculateMonth(
            TestPlans.Month(), 0, decimal.Parse(value, CultureInfo.InvariantCulture)));
        Assert.Equal("OpeningDebt", error.Field);
    }
}
