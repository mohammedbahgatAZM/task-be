namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ArticleRepository(SupportCrmDbContext dbContext) : IArticleRepository
{
    public Task<Article?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.Articles.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Article>> GetAllAsync(bool includeUnpublished, CancellationToken ct) =>
        await Filter(dbContext.Articles, includeUnpublished).ToListAsync(ct);

    public async Task<IReadOnlyList<Article>> GetByCategoryAsync(Guid kbCategoryId, bool includeUnpublished, CancellationToken ct) =>
        await Filter(dbContext.Articles.Where(a => a.KbCategoryId == kbCategoryId), includeUnpublished).ToListAsync(ct);

    private static IQueryable<Article> Filter(IQueryable<Article> query, bool includeUnpublished) =>
        includeUnpublished ? query : query.Where(a => a.Status == KbContentStatus.Published);

    public async Task<IReadOnlyList<Article>> SearchPublishedAsync(string query, CancellationToken ct) =>
        await dbContext.Articles
            .Where(a => a.Status == KbContentStatus.Published)
            .Where(a =>
                EF.Functions.ILike(a.TitleEn ?? "", $"%{query}%") || EF.Functions.ILike(a.TitleAr ?? "", $"%{query}%") ||
                EF.Functions.ILike(a.BodyEn ?? "", $"%{query}%") || EF.Functions.ILike(a.BodyAr ?? "", $"%{query}%"))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Article>> GetDueForReviewAsync(DateTimeOffset asOfUtc, CancellationToken ct) =>
        await dbContext.Articles.Where(a => a.ReviewDueAtUtc != null && a.ReviewDueAtUtc <= asOfUtc).ToListAsync(ct);

    public Task AddAsync(Article article, CancellationToken ct)
    {
        dbContext.Articles.Add(article);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
