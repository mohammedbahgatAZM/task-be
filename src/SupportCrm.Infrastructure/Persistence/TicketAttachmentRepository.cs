namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketAttachmentRepository(SupportCrmDbContext dbContext) : ITicketAttachmentRepository
{
    public Task<TicketAttachment?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.TicketAttachments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<TicketAttachment>> GetByTicketAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.TicketAttachments.Where(a => a.TicketId == ticketId).ToListAsync(ct);

    public Task AddAsync(TicketAttachment attachment, CancellationToken ct)
    {
        dbContext.TicketAttachments.Add(attachment);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
