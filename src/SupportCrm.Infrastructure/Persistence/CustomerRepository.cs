namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class CustomerRepository(SupportCrmDbContext dbContext) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Customer?> GetByCustomerNumberAsync(string customerNumber, CancellationToken ct) =>
        dbContext.Customers.FirstOrDefaultAsync(c => c.CustomerNumber == customerNumber, ct);

    public async Task<IReadOnlyList<Customer>> SearchAsync(string query, int take, CancellationToken ct) =>
        await dbContext.Customers
            .Where(c => c.Name.Contains(query) || (c.Company != null && c.Company.Contains(query)))
            .OrderBy(c => c.Name)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct) =>
        await dbContext.Customers.ToListAsync(ct);

    public Task AddAsync(Customer customer, CancellationToken ct)
    {
        dbContext.Customers.Add(customer);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
