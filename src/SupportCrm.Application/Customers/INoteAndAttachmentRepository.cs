namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;

public interface INoteAndAttachmentRepository
{
    Task<IReadOnlyList<CustomerNote>> GetNotesAsync(Guid customerId, CancellationToken ct);
    Task<CustomerNote?> GetNoteByIdAsync(Guid noteId, CancellationToken ct);
    Task AddNoteAsync(CustomerNote note, CancellationToken ct);

    Task<IReadOnlyList<CustomerAttachment>> GetAttachmentsAsync(Guid customerId, CancellationToken ct);
    Task<CustomerAttachment?> GetAttachmentByIdAsync(Guid attachmentId, CancellationToken ct);
    Task AddAttachmentAsync(CustomerAttachment attachment, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
