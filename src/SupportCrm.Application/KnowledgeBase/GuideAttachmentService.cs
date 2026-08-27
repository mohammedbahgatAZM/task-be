namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class GuideAttachmentService(
    IGuideRepository guideRepository,
    IGuideAttachmentRepository attachmentRepository,
    IGuideAttachmentStorage storage,
    TimeProvider timeProvider)
{
    public async Task<GuideAttachmentDto> AddAsync(Guid guideId, string fileName, string contentType, long sizeBytes, Stream content, string uploadedByName, CancellationToken ct)
    {
        _ = await guideRepository.GetByIdAsync(guideId, ct) ?? throw new GuideNotFoundException(guideId.ToString());

        var attachmentId = Guid.NewGuid();
        var storageKey = await storage.SaveAsync(guideId, attachmentId, fileName, content, ct);

        var attachment = new GuideAttachment(guideId, fileName, contentType, sizeBytes, storageKey, uploadedByName, timeProvider.GetUtcNow());
        await attachmentRepository.AddAsync(attachment, ct);
        await attachmentRepository.SaveChangesAsync(ct);
        return ToDto(attachment);
    }

    public async Task<IReadOnlyList<GuideAttachmentDto>> GetForArticleAsync(Guid guideId, CancellationToken ct) =>
        (await attachmentRepository.GetByGuideAsync(guideId, ct)).Select(ToDto).ToList();

    public async Task<(Stream Content, GuideAttachment Attachment)> OpenAsync(Guid attachmentId, CancellationToken ct)
    {
        var attachment = await attachmentRepository.GetByIdAsync(attachmentId, ct) ?? throw new KeyNotFoundException($"Attachment '{attachmentId}' was not found.");
        var stream = await storage.OpenReadAsync(attachment.StorageKey, ct);
        return (stream, attachment);
    }

    private static GuideAttachmentDto ToDto(GuideAttachment a) => new(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByName, a.UploadedAtUtc);
}
