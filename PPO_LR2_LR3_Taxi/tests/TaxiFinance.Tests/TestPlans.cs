using TaxiFinance.Domain;
namespace TaxiFinance.Tests;
internal static class TestPlans
{
    public static readonly Guid A = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly Guid B = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public static MonthParameters Month(int number, int percent = 50) => new(number, 500, 300m, 100m, 70000m, percent);
    public static FinancialPlan Standard(int percent = 50, Guid? id = null, decimal cash = 50000m) =>
        new(id ?? A, $"Такси — {percent}% сразу", cash, Enumerable.Range(1, 3).Select(n => Month(n, percent)));
    public static FinancialPlan WithMonths(IEnumerable<MonthParameters> months) => new(A, "Такси", 50000m, months);
}
