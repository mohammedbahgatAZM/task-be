namespace SupportCrm.Application.Customers;

using Microsoft.Extensions.Options;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class NoteAndAttachmentService(
    ICustomerRepository customerRepository,
    INoteAndAttachmentRepository repository,
    IAttachmentStorage attachmentStorage,
    IOptions<AttachmentOptions> attachmentOptions,
    TimeProvider timeProvider)
{
    public async Task<NoteDto> AddNoteAsync(Guid customerId, AddNoteRequest request, CancellationToken ct)
    {
        _ = await customerRepository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);

        var note = new CustomerNote(customerId, request.Text.Trim(), request.AuthorName, timeProvider.GetUtcNow());
        if (request.IsPinned) note.SetPinned(true);

        await repository.AddNoteAsync(note, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(note);
    }

    public async Task SetNotePinnedAsync(Guid noteId, SetNotePinnedRequest request, CancellationToken ct)
    {
        var note = await repository.GetNoteByIdAsync(noteId, ct) ?? throw new KeyNotFoundException($"Note '{noteId}' was not found.");
        note.SetPinned(request.IsPinned);
        await repository.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NoteDto>> GetNotesAsync(Guid customerId, CancellationToken ct) =>
        (await repository.GetNotesAsync(customerId, ct))
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAtUtc)
            .Select(ToDto)
            .ToList();

    public async Task<AttachmentDto> AddAttachmentAsync(Guid customerId, string fileName, string contentType, long sizeBytes, Stream content, string uploadedByName, CancellationToken ct)
    {
        _ = await customerRepository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);

        var maxSize = attachmentOptions.Value.MaxSizeBytes;
        if (sizeBytes > maxSize)
            throw new AttachmentTooLargeException(sizeBytes, maxSize);

        var attachmentId = Guid.NewGuid();
        var storageKey = await attachmentStorage.SaveAsync(customerId, attachmentId, fileName, content, ct);

        var attachment = new CustomerAttachment(customerId, fileName, contentType, sizeBytes, storageKey, uploadedByName, timeProvider.GetUtcNow());
        await repository.AddAttachmentAsync(attachment, ct);
        await repository.SaveChangesAsync(ct);
        return ToAttachmentDto(attachment);
    }

    public async Task<IReadOnlyList<AttachmentDto>> GetAttachmentsAsync(Guid customerId, CancellationToken ct) =>
        (await repository.GetAttachmentsAsync(customerId, ct)).Select(ToAttachmentDto).ToList();

    public async Task<(Stream Content, CustomerAttachment Attachment)> OpenAttachmentAsync(Guid attachmentId, CancellationToken ct)
    {
        var attachment = await repository.GetAttachmentByIdAsync(attachmentId, ct) ?? throw new KeyNotFoundException($"Attachment '{attachmentId}' was not found.");
        var stream = await attachmentStorage.OpenReadAsync(attachment.StorageKey, ct);
        return (stream, attachment);
    }

    private static NoteDto ToDto(CustomerNote n) => new(n.Id, n.Text, n.AuthorName, n.IsPinned, n.CreatedAtUtc);
    private static AttachmentDto ToAttachmentDto(CustomerAttachment a) => new(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByName, a.UploadedAtUtc);
}
