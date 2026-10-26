namespace Taxi.Domain;
public enum OrderStatus { Created, Assigned, InProgress, Completed }
public sealed class RuleException(string field, string message) : Exception(message) { public string Field { get; } = field; }
public sealed record Tariff(string Id, string Name, decimal BasePrice, decimal Rate, bool Active = true);
public sealed record Quote(string TariffId, string Name, decimal BasePrice, decimal VariablePrice, decimal Total);
public sealed record TripInput(string FullName, string Phone, string Pickup, string Destination, decimal DistanceKm, string TariffId)
{
    public void Validate()
    {
        Required(FullName, "FullName", 150); Required(Phone, "Phone", 50);
        Required(Pickup, "Pickup", 500); Required(Destination, "Destination", 500);
        QuoteService.ValidateDistance(DistanceKm);
        Required(TariffId, "TariffId", 100);
    }
    private static void Required(string text, string field, int max)
    { if (string.IsNullOrWhiteSpace(text) || text.Length > max) throw new RuleException(field, $"Обязательное поле, не более {max} символов."); }
}
public sealed record Order(string Id, string ClientId, string? DriverId, string TariffId,
    string Pickup, string Destination, decimal DistanceKm, decimal BasePrice, decimal Rate,
    decimal Cost, OrderStatus Status, DateTimeOffset CreatedAt);
public sealed record Driver(string Id, string FullName, string CarName, string CarNumber, bool Available);
public sealed class QuoteService
{
    public static void ValidateDistance(decimal distance)
    { if (distance <= 0 || distance > 1000 || decimal.Round(distance, 2) != distance) throw new RuleException("DistanceKm", "Расстояние: от 0.01 до 1000 км, до двух знаков."); }
    public Quote Calculate(Tariff tariff, decimal distance)
    {
        ValidateDistance(distance);
        if (!tariff.Active || tariff.BasePrice < 0 || tariff.Rate < 0 || tariff.BasePrice > 1000000 || tariff.Rate > 1000000 || decimal.Round(tariff.BasePrice,2) != tariff.BasePrice || decimal.Round(tariff.Rate,2) != tariff.Rate)
            throw new RuleException("Tariff", "Тариф недоступен или содержит некорректные цены.");
        decimal variable = distance * tariff.Rate;
        return new(tariff.Id, tariff.Name, tariff.BasePrice, variable,
            decimal.Round(tariff.BasePrice + variable, 2, MidpointRounding.AwayFromZero));
    }
    public IReadOnlyList<Quote> Compare(IEnumerable<Tariff> tariffs, decimal distance)
    {
        ValidateDistance(distance);
        var result = tariffs.Where(t => t.Active).Select(t => Calculate(t, distance)).ToArray();
        if (result.Length == 0) throw new RuleException("Tariff", "Нет доступных тарифов.");
        return result;
    }
}
