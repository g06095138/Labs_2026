namespace TelecomFinance.Domain;

public static class PlanValidator
{
    public const decimal MaxInputMoney = 1_000_000_000_000m;
    public const int MaxQuantity = 1_000_000;

    public static void Validate(FinancialPlan plan)
    {
        if (plan is null) throw new FinanceValidationException("Plan", "План не задан.");
        if (plan.Id == Guid.Empty) throw new FinanceValidationException("Id", "Идентификатор не должен быть пустым.");
        if (string.IsNullOrWhiteSpace(plan.Name) || plan.Name.Length > 200)
            throw new FinanceValidationException("Name", "Название должно содержать от 1 до 200 символов.");
        Money(plan.InitialCash, "InitialCash");
        if (plan.StartYear is < 2020 or > 2099)
            throw new FinanceValidationException("StartYear", "Год начала: от 2020 до 2099.");
        if (plan.StartMonth is < 1 or > 12)
            throw new FinanceValidationException("StartMonth", "Месяц начала: от 1 до 12.");
        if (new DateOnly(plan.StartYear, plan.StartMonth, 1).AddMonths(2).Year > 2099)
            throw new FinanceValidationException("StartYear", "Все три месяца должны находиться в пределах 2020–2099 годов.");
        if (plan.Months.Count != 3) throw new FinanceValidationException("Months", "Нужны ровно три месяца.");
        foreach (var month in plan.Months)
        {
            if (month is null) throw new FinanceValidationException("Months", "Месяц не должен быть пустым.");
            ValidateMonth(month);
        }
        if (plan.Months.Select(m => m.Number).Distinct().Count() != 3)
            throw new FinanceValidationException("Months.Number", "Номера 1, 2, 3 должны встречаться по одному разу.");
    }

    public static void ValidateMonth(MonthParameters month)
    {
        if (month is null) throw new FinanceValidationException("Month", "Месяц не задан.");
        var prefix = $"Months[{month.Number}]";
        if (month.Number is < 1 or > 3) throw new FinanceValidationException("Months.Number", "Номер месяца: 1, 2 или 3.");
        if (month.ActiveClients is < 0 or > MaxQuantity)
            throw new FinanceValidationException(prefix + ".ActiveClients", $"Количество абонентов: от 0 до {MaxQuantity}.");
        if (month.AvgBillableMinutesPerClient is < 0 or > MaxQuantity)
            throw new FinanceValidationException(prefix + ".AvgBillableMinutesPerClient", $"Средние оплачиваемые минуты: целое число от 0 до {MaxQuantity}.");
        Money(month.PricePerMinute, prefix + ".PricePerMinute");
        Money(month.MonthlyFeePerClient, prefix + ".MonthlyFeePerClient");
        Money(month.VariableCostPerClient, prefix + ".VariableCostPerClient");
        Money(month.OperationalCosts, prefix + ".OperationalCosts");
        if (month.PaymentPercent is < 0 or > 100)
            throw new FinanceValidationException(prefix + ".PaymentPercent", "Введите целый процент от 0 до 100.");
    }

    private static void Money(decimal value, string field)
    {
        if (value < 0 || value > MaxInputMoney || decimal.Round(value, 2) != value)
            throw new FinanceValidationException(field, $"Сумма должна быть от 0 до {MaxInputMoney}, не более двух знаков после запятой.");
    }
}
