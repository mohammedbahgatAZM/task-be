namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Customer?> GetByCustomerNumberAsync(string customerNumber, CancellationToken ct);
    Task<IReadOnlyList<Customer>> SearchAsync(string query, int take, CancellationToken ct);
    // Reports & Management — full listing for branch/department lookups. Same
    // in-memory-filtering scale tradeoff already flagged on ITicketRepository.GetAllAsync.
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct);
    Task AddAsync(Customer customer, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
