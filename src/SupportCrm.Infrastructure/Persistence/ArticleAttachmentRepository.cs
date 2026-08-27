namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ArticleAttachmentRepository(SupportCrmDbContext dbContext) : IArticleAttachmentRepository
{
    public Task<ArticleAttachment?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.ArticleAttachments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<ArticleAttachment>> GetByArticleAsync(Guid articleId, CancellationToken ct) =>
        await dbContext.ArticleAttachments.Where(a => a.ArticleId == articleId).ToListAsync(ct);

    public Task AddAsync(ArticleAttachment attachment, CancellationToken ct)
    {
        dbContext.ArticleAttachments.Add(attachment);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
