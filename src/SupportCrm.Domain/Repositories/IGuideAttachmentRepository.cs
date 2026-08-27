namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IGuideAttachmentRepository
{
    Task<GuideAttachment?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<GuideAttachment>> GetByGuideAsync(Guid guideId, CancellationToken ct);
    Task AddAsync(GuideAttachment attachment, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
