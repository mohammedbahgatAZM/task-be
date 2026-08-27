namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketAttachmentRepository
{
    Task<TicketAttachment?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<TicketAttachment>> GetByTicketAsync(Guid ticketId, CancellationToken ct);
    Task AddAsync(TicketAttachment attachment, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
