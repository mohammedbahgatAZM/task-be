namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IArticleRepository
{
    Task<Article?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Article>> GetAllAsync(bool includeUnpublished, CancellationToken ct);
    Task<IReadOnlyList<Article>> GetByCategoryAsync(Guid kbCategoryId, bool includeUnpublished, CancellationToken ct);
    Task<IReadOnlyList<Article>> SearchPublishedAsync(string query, CancellationToken ct);
    Task<IReadOnlyList<Article>> GetDueForReviewAsync(DateTimeOffset asOfUtc, CancellationToken ct);
    Task AddAsync(Article article, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
