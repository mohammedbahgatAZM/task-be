namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;

public interface IContactDetailRepository
{
    Task<ContactDetail?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ContactDetail?> FindByValueAsync(string value, CancellationToken ct);
    Task<IReadOnlyList<ContactDetail>> GetByCustomerAsync(Guid customerId, CancellationToken ct);
    Task<IReadOnlyList<ContactDetailChangeLogEntry>> GetChangeLogAsync(Guid customerId, CancellationToken ct);
    Task AddAsync(ContactDetail contactDetail, CancellationToken ct);
    Task AddChangeLogAsync(ContactDetailChangeLogEntry entry, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
