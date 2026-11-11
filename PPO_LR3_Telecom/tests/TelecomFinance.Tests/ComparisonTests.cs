using TelecomFinance.Application;
using TelecomFinance.Domain;
using Xunit;

namespace TelecomFinance.Tests;

public sealed class ComparisonTests
{
    private readonly ForecastService forecasts = new(new FinancialCalculator());

    [Fact]
    public void CompareIndependentPlans_ReturnsLiteralExpectedTotals_WithoutMutation()
    {
        var left = TestPlans.Standard(0, TestPlans.A);
        var right = TestPlans.Standard(50, TestPlans.B);
        var beforeLeft = left.Months.ToArray();
        var beforeRight = right.Months.ToArray();
        var result = new ComparisonService(forecasts).Compare(left, right);
        Assert.Equal(left.Id, result.Left.PlanId);
        Assert.Equal(right.Id, result.Right.PlanId);
        Assert.Equal(new FinancialSummary(90000, -10000, 150000, 70000), result.Left.Summary);
        Assert.Equal(new FinancialSummary(90000, 65000, 75000, 0), result.Right.Summary);
        Assert.Equal(beforeLeft, left.Months.ToArray());
        Assert.Equal(beforeRight, right.Months.ToArray());
        Assert.Equal(50000m, left.InitialCash);
        Assert.Equal(50000m, right.InitialCash);
    }

    [Fact]
    public void CompareInReverseOrder_DoesNotMixCarryBetweenPlans()
    {
        var left = TestPlans.Standard(0, TestPlans.A);
        var right = TestPlans.Standard(50, TestPlans.B);
        var comparisons = new ComparisonService(forecasts);
        var first = comparisons.Compare(left, right);
        var reversed = comparisons.Compare(right, left);
        Assert.Equal(first.Left, reversed.Right);
        Assert.Equal(first.Right, reversed.Left);
        Assert.Equal(first, comparisons.Compare(left, right));
    }

    [Fact]
    public void ChangingInitialCash_RecalculatesOneSide_WithoutChangingOtherPlan()
    {
        var left = TestPlans.Standard(0, TestPlans.A);
        var right = TestPlans.Standard(50, TestPlans.B);
        var comparisons = new ComparisonService(forecasts);
        var before = comparisons.Compare(left, right);
        var editedLeft = new FinancialPlan(left.Id, left.Name, 120000,
            left.StartYear, left.StartMonth, left.Months);
        var forecast = forecasts.Forecast(editedLeft);
        var after = comparisons.Compare(editedLeft, right);

        Assert.Equal(new[] { 0m, 30000m, 60000m }, forecast.Months.Select(m => m.ClosingCash));
        Assert.Equal(new FinancialSummary(90000, 60000, 150000, 0), after.Left.Summary);
        Assert.Equal(before.Right, after.Right);
        Assert.Equal(new FinancialSummary(90000, 65000, 75000, 0), after.Right.Summary);
        Assert.Equal(50000m, left.InitialCash);
        Assert.Equal(120000m, editedLeft.InitialCash);
        Assert.Equal(new[] { -70000m, -40000m, -10000m }, forecasts.Forecast(left).Months.Select(m => m.ClosingCash));
        Assert.Equal(before, comparisons.Compare(left, right));
    }

    [Fact]
    public void ComparingSameIdentifier_IsRejected()
    {
        var error = Assert.Throws<FinanceValidationException>(() => new ComparisonService(forecasts)
            .Compare(TestPlans.Standard(0), TestPlans.Standard(50)));
        Assert.Equal("Plans", error.Field);
    }

    [Theory]
    [InlineData(true, "Left.InitialCash")]
    [InlineData(false, "Right.InitialCash")]
    public void InvalidComparisonPlan_IdentifiesTheSide(bool invalidLeft, string expectedField)
    {
        var valid = TestPlans.Standard(50, TestPlans.B);
        var invalid = new FinancialPlan(TestPlans.A, "Ошибка", -1, 2026, 11, valid.Months);
        var error = Assert.Throws<FinanceValidationException>(() => new ComparisonService(forecasts)
            .Compare(invalidLeft ? invalid : valid, invalidLeft ? valid : invalid));
        Assert.Equal(expectedField, error.Field);
    }
}
