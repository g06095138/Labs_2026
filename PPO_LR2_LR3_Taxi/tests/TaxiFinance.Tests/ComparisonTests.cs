using TaxiFinance.Application;
using TaxiFinance.Domain;
using Xunit;
namespace TaxiFinance.Tests;

public sealed class ComparisonTests
{
    private readonly ComparisonService comparisons = new(new ForecastService(new FinancialCalculator()));
    [Fact]
    public void Compare_ReturnsIndependentTotals_AndLeavesBothPlansUnchanged()
    {
        var a=TestPlans.Standard(0,TestPlans.A);var b=TestPlans.Standard(50,TestPlans.B);
        var aMonths=a.Months.ToArray();var bMonths=b.Months.ToArray();
        var result=comparisons.Compare(a,b);
        Assert.Equal(new FinancialSummary(90000,-10000,150000,70000),result.Left.Summary);
        Assert.Equal(new FinancialSummary(90000,65000,75000,0),result.Right.Summary);
        Assert.Equal(TestPlans.A,result.Left.PlanId);Assert.Equal(TestPlans.B,result.Right.PlanId);
        Assert.Equal(aMonths,a.Months.ToArray());Assert.Equal(bMonths,b.Months.ToArray());
        Assert.Equal(result,comparisons.Compare(a,b));
    }
    [Fact]
    public void Compare_DifferentStartingCash_DoesNotLeakBetweenSides()
    {
        var a=TestPlans.Standard(0,TestPlans.A,cash:120000m);var b=TestPlans.Standard(50,TestPlans.B);
        var result=comparisons.Compare(a,b);
        Assert.Equal(new FinancialSummary(90000,60000,150000,0),result.Left.Summary);
        Assert.Equal(new FinancialSummary(90000,65000,75000,0),result.Right.Summary);
    }
    [Fact] public void Compare_SameId_IsRejected() => Assert.Equal("Plans",Assert.Throws<FinanceValidationException>(()=>comparisons.Compare(TestPlans.Standard(0),TestPlans.Standard(50))).Field);
    [Fact] public void Compare_InvalidLeft_IsLabelled() => Assert.Equal("Left.InitialCash",Assert.Throws<FinanceValidationException>(()=>comparisons.Compare(TestPlans.Standard(0,cash:-1),TestPlans.Standard(50,TestPlans.B))).Field);
    [Fact] public void Compare_InvalidRight_IsLabelled() => Assert.Equal("Right.InitialCash",Assert.Throws<FinanceValidationException>(()=>comparisons.Compare(TestPlans.Standard(0),TestPlans.Standard(50,TestPlans.B,cash:-1))).Field);
}
