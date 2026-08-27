namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class GuideAttachmentRepository(SupportCrmDbContext dbContext) : IGuideAttachmentRepository
{
    public Task<GuideAttachment?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.GuideAttachments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<GuideAttachment>> GetByGuideAsync(Guid guideId, CancellationToken ct) =>
        await dbContext.GuideAttachments.Where(a => a.GuideId == guideId).ToListAsync(ct);

    public Task AddAsync(GuideAttachment attachment, CancellationToken ct)
    {
        dbContext.GuideAttachments.Add(attachment);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
