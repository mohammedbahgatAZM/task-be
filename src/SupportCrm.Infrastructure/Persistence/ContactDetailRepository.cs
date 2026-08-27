namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Customers;
using SupportCrm.Domain.Entities;

public class ContactDetailRepository(SupportCrmDbContext dbContext) : IContactDetailRepository
{
    public Task<ContactDetail?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.ContactDetails.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<ContactDetail?> FindByValueAsync(string value, CancellationToken ct) =>
        dbContext.ContactDetails.FirstOrDefaultAsync(c => c.Value == value, ct);

    public async Task<IReadOnlyList<ContactDetail>> GetByCustomerAsync(Guid customerId, CancellationToken ct) =>
        await dbContext.ContactDetails.Where(c => c.CustomerId == customerId).ToListAsync(ct);

    public async Task<IReadOnlyList<ContactDetailChangeLogEntry>> GetChangeLogAsync(Guid customerId, CancellationToken ct) =>
        await dbContext.ContactDetailChangeLogEntries.Where(e => e.CustomerId == customerId).ToListAsync(ct);

    public Task AddAsync(ContactDetail contactDetail, CancellationToken ct)
    {
        dbContext.ContactDetails.Add(contactDetail);
        return Task.CompletedTask;
    }

    public Task AddChangeLogAsync(ContactDetailChangeLogEntry entry, CancellationToken ct)
    {
        dbContext.ContactDetailChangeLogEntries.Add(entry);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
