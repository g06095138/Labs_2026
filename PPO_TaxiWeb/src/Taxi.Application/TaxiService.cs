using Taxi.Domain;
namespace Taxi.Application;
public enum Role { Client, Dispatcher, Driver }
public sealed record Actor(string UserId, Role Role, string? ClientId = null, string? DriverId = null);
public interface ITaxiStore
{
    IReadOnlyList<Tariff> Tariffs();
    IReadOnlyList<Driver> Drivers();
    IReadOnlyList<Order> Orders();
    Order? Get(string id);
    Order Create(string clientId, TripInput input);
    void Assign(string orderId, string driverId);
    void Transition(string orderId, string driverId, OrderStatus from, OrderStatus to);
}
public sealed class TaxiService(ITaxiStore store, QuoteService calculator)
{
    public IReadOnlyList<Quote> Compare(Actor actor, decimal distance)
    { Require(actor, Role.Client); return calculator.Compare(store.Tariffs(), distance); }
    public Order Create(Actor actor, TripInput input)
    {
        Require(actor, Role.Client); input.Validate();
        if (actor.ClientId is null) throw new RuleException("User", "Нет профиля клиента.");
        return store.Create(actor.ClientId, input);
    }
    public IReadOnlyList<Order> List(Actor actor) => store.Orders().Where(o => actor.Role switch {
        Role.Dispatcher => true, Role.Client => actor.ClientId is not null && o.ClientId == actor.ClientId,
        Role.Driver => actor.DriverId is not null && o.DriverId == actor.DriverId && o.Status is OrderStatus.Assigned or OrderStatus.InProgress,
        _ => false }).ToArray();
    public Order Get(Actor actor, string id)
    {
        var order = store.Get(id) ?? throw new RuleException("Order", "Заказ не найден.");
        bool allowed = actor.Role == Role.Dispatcher || (actor.Role == Role.Client && order.ClientId == actor.ClientId) || (actor.Role == Role.Driver && order.DriverId == actor.DriverId);
        if (!allowed) throw new RuleException("Role", "Нет доступа к заказу.");
        return order;
    }
    public IReadOnlyList<Driver> FreeDrivers(Actor actor)
    { Require(actor, Role.Dispatcher); return store.Drivers().Where(d => d.Available).ToArray(); }
    public void Assign(Actor actor, string id, string driverId)
    { Require(actor, Role.Dispatcher); store.Assign(id, driverId); }
    public void Start(Actor actor, string id) => Move(actor, id, OrderStatus.Assigned, OrderStatus.InProgress);
    public void Complete(Actor actor, string id) => Move(actor, id, OrderStatus.InProgress, OrderStatus.Completed);
    private void Move(Actor actor, string id, OrderStatus from, OrderStatus to)
    {
        Require(actor, Role.Driver);
        if (actor.DriverId is null) throw new RuleException("User", "Нет профиля водителя.");
        store.Transition(id, actor.DriverId, from, to);
    }
    private static void Require(Actor actor, Role role)
    { if (actor.Role != role) throw new RuleException("Role", "Действие недоступно для вашей роли."); }
}
