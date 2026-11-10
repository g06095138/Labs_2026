using TaxiFinance.Domain;
namespace TaxiFinance.Application;

public sealed class ComparisonService(ForecastService forecasts)
{
    public PlanComparison Compare(FinancialPlan left, FinancialPlan right)
    {
        ValidateSide(left, "Left");
        ValidateSide(right, "Right");
        if (left.Id == right.Id)
            throw new FinanceValidationException("Plans", "Выберите два разных плана.");
        var a = forecasts.Forecast(left);
        var b = forecasts.Forecast(right);
        return new(new(a.PlanId, a.PlanName, a.Summary), new(b.PlanId, b.PlanName, b.Summary));
    }

    private static void ValidateSide(FinancialPlan plan, string side)
    {
        try { PlanValidator.Validate(plan); }
        catch (FinanceValidationException ex)
        { throw new FinanceValidationException(side + "." + ex.Field, ex.Message); }
    }
}
