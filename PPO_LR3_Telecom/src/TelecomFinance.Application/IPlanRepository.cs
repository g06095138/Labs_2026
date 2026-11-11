using TelecomFinance.Domain;
namespace TelecomFinance.Application;

public sealed record PlanListItem(Guid Id, string Name);

// Контракт ЛР2. Реальная реализация и её интеграционные тесты относятся к ЛР4.
public interface IPlanRepository
{
    Task SaveAsync(FinancialPlan plan, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlanListItem>> ListAsync(CancellationToken cancellationToken = default);
    Task<FinancialPlan?> LoadAsync(Guid id, CancellationToken cancellationToken = default);
}
