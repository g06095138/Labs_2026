using TaxiFinance.Application;
using TaxiFinance.Domain;
using Xunit;
namespace TaxiFinance.Tests;

public sealed class ValidationTests
{
    private readonly ForecastService forecasts = new(new FinancialCalculator());
    [Fact] public void NegativeInitialCash_IsRejected() => Assert.Equal("InitialCash", Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(TestPlans.Standard(cash: -0.01m))).Field);
    [Theory][InlineData(null)][InlineData("")][InlineData(" ")][InlineData("\t\n")]
    public void EmptyName_IsRejected(string? name)
    {
        var plan = new FinancialPlan(TestPlans.A, name!, 0m, TestPlans.Standard().Months);
        Assert.Equal("Name", Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(plan)).Field);
    }
    [Fact] public void EmptyId_IsRejected() => Assert.Equal("Id", Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(TestPlans.Standard(id: Guid.Empty))).Field);
    [Fact] public void NullPlan_IsRejected() => Assert.Equal("Plan", Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(null!)).Field);
    [Fact] public void NullMonthCollection_IsRejected() => Assert.Equal("Months", Assert.Throws<FinanceValidationException>(() => new FinancialPlan(TestPlans.A,"Такси",0,null!)).Field);
    [Fact] public void NullMonth_IsRejected() => Assert.Equal("Months", Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(TestPlans.WithMonths(new[] {TestPlans.Month(1),null!,TestPlans.Month(3)}))).Field);

    public static TheoryData<int[]> InvalidMonthSets => new()
    {
        Array.Empty<int>(), new[] { 1 }, new[] { 1, 2 }, new[] { 1, 2, 3, 4 },
        new[] { 1, 1, 3 }, new[] { 0, 2, 3 }, new[] { 1, 2, 4 }, new[] { 2, 2, 2 }
    };
    [Theory][MemberData(nameof(InvalidMonthSets))]
    public void InvalidMonthSet_IsRejected(int[] numbers)
    {
        var plan = TestPlans.WithMonths(numbers.Select(n => TestPlans.Month(n)));
        Assert.StartsWith("Months", Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(plan)).Field);
    }

    public static TheoryData<MonthParameters, string> BadMonths => new()
    {
        {TestPlans.Month(2) with {Quantity=-1}, "Quantity"},
        {TestPlans.Month(2) with {Quantity=1000001}, "Quantity"},
        {TestPlans.Month(2) with {Price=-0.01m}, "Price"},
        {TestPlans.Month(2) with {VariableCost=-0.01m}, "VariableCost"},
        {TestPlans.Month(2) with {FixedCost=-0.01m}, "FixedCost"},
        {TestPlans.Month(2) with {Price=0.001m}, "Price"},
        {TestPlans.Month(2) with {VariableCost=0.001m}, "VariableCost"},
        {TestPlans.Month(2) with {FixedCost=0.001m}, "FixedCost"},
        {TestPlans.Month(2) with {Price=1000000000000.01m}, "Price"},
        {TestPlans.Month(2) with {VariableCost=1000000000000.01m}, "VariableCost"},
        {TestPlans.Month(2) with {FixedCost=1000000000000.01m}, "FixedCost"},
        {TestPlans.Month(2) with {PaymentPercent=-1}, "PaymentPercent"},
        {TestPlans.Month(2) with {PaymentPercent=101}, "PaymentPercent"}
    };
    [Theory][MemberData(nameof(BadMonths))]
    public void InvalidMonthlyField_IsReportedBeforeReturningForecast(MonthParameters bad, string field)
    {
        var plan = TestPlans.WithMonths(new[] {TestPlans.Month(1),bad,TestPlans.Month(3)});
        Assert.Equal("Months[2]."+field, Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(plan)).Field);
    }
    [Fact] public void ExcessInitialPrecision_IsRejected() => Assert.Equal("InitialCash", Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(TestPlans.Standard(cash:0.001m))).Field);
    [Fact] public void ExcessInitialRange_IsRejected() => Assert.Equal("InitialCash", Assert.Throws<FinanceValidationException>(() => forecasts.Forecast(TestPlans.Standard(cash:1000000000000.01m))).Field);
    [Fact] public void TrailingZeroPrecision_IsAccepted() { var r=forecasts.Forecast(TestPlans.Standard(cash:1.000m)); Assert.Equal(1m,r.Months[0].OpeningCash); }
    [Fact] public void MaximumInputs_CalculateWithoutOverflow()
    {
        var plan=new FinancialPlan(TestPlans.A,"Граница",1000000000000m,Enumerable.Range(1,3).Select(n=>new MonthParameters(n,1000000,1000000000000m,0,0,100)));
        var r=forecasts.Forecast(plan);
        Assert.Equal(3000000000000000000m,r.Summary.TotalProfit);
        Assert.Equal(3000001000000000000m,r.Summary.FinalCash);
        Assert.Equal(0m,r.Summary.FinalDebt);
    }
}
