using TelecomFinance.Domain;

namespace TelecomFinance.Tests;

internal static class TestPlans
{
    public static readonly Guid A = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly Guid B = Guid.Parse("22222222-2222-4222-8222-222222222222");

    public static MonthParameters Month(int number = 1, int percent = 50) =>
        new(number, 100, 200, 5m, 500m, 500m, 70000m, percent);

    public static FinancialPlan Standard(int percent = 50, Guid? id = null) =>
        new(id ?? A, "Оператор связи", 50000m, 2026, 11,
            Enumerable.Range(1, 3).Select(n => Month(n, percent)));

    public static FinancialPlan WithMonths(IEnumerable<MonthParameters> months) =>
        new(A, "Оператор связи", 50000m, 2026, 11, months);
}
