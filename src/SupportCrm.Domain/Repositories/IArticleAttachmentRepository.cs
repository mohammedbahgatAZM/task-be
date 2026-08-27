namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IArticleAttachmentRepository
{
    Task<ArticleAttachment?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ArticleAttachment>> GetByArticleAsync(Guid articleId, CancellationToken ct);
    Task AddAsync(ArticleAttachment attachment, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
