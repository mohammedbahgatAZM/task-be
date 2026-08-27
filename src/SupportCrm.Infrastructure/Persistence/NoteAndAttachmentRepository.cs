namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Customers;
using SupportCrm.Domain.Entities;

public class NoteAndAttachmentRepository(SupportCrmDbContext dbContext) : INoteAndAttachmentRepository
{
    public async Task<IReadOnlyList<CustomerNote>> GetNotesAsync(Guid customerId, CancellationToken ct) =>
        await dbContext.CustomerNotes.Where(n => n.CustomerId == customerId).ToListAsync(ct);

    public Task<CustomerNote?> GetNoteByIdAsync(Guid noteId, CancellationToken ct) =>
        dbContext.CustomerNotes.FirstOrDefaultAsync(n => n.Id == noteId, ct);

    public Task AddNoteAsync(CustomerNote note, CancellationToken ct)
    {
        dbContext.CustomerNotes.Add(note);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<CustomerAttachment>> GetAttachmentsAsync(Guid customerId, CancellationToken ct) =>
        await dbContext.CustomerAttachments.Where(a => a.CustomerId == customerId).ToListAsync(ct);

    public Task<CustomerAttachment?> GetAttachmentByIdAsync(Guid attachmentId, CancellationToken ct) =>
        dbContext.CustomerAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId, ct);

    public Task AddAttachmentAsync(CustomerAttachment attachment, CancellationToken ct)
    {
        dbContext.CustomerAttachments.Add(attachment);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
