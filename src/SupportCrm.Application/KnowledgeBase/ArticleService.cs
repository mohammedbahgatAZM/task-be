namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ArticleService(IArticleRepository repository, TimeProvider timeProvider)
{
    public async Task<ArticleDto> CreateAsync(CreateArticleRequest request, CancellationToken ct)
    {
        var article = new Article(request.KbCategoryId, request.TitleEn?.Trim(), request.TitleAr?.Trim(),
            request.BodyEn?.Trim(), request.BodyAr?.Trim(), request.AuthorName.Trim(), timeProvider.GetUtcNow());
        await repository.AddAsync(article, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(article);
    }

    // Increments the view count — call only from the single-article read, not list reads.
    public async Task<ArticleDto> GetByIdAndTrackViewAsync(Guid id, CancellationToken ct)
    {
        var article = await repository.GetByIdAsync(id, ct) ?? throw new ArticleNotFoundException(id.ToString());
        article.IncrementViewCount();
        await repository.SaveChangesAsync(ct);
        return ToDto(article);
    }

    public async Task<IReadOnlyList<ArticleDto>> GetAllAsync(bool includeUnpublished, CancellationToken ct) =>
        (await repository.GetAllAsync(includeUnpublished, ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<ArticleDto>> GetByCategoryAsync(Guid kbCategoryId, bool includeUnpublished, CancellationToken ct) =>
        (await repository.GetByCategoryAsync(kbCategoryId, includeUnpublished, ct)).Select(ToDto).ToList();

    public async Task<ArticleDto> UpdateAsync(Guid id, UpdateArticleRequest request, CancellationToken ct)
    {
        var article = await repository.GetByIdAsync(id, ct) ?? throw new ArticleNotFoundException(id.ToString());
        article.RecordUpdate(request.TitleEn?.Trim(), request.TitleAr?.Trim(), request.BodyEn?.Trim(), request.BodyAr?.Trim(), request.ChangedBy, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(ct);
        return ToDto(article);
    }

    public async Task MarkHelpfulAsync(Guid id, CancellationToken ct)
    {
        var article = await repository.GetByIdAsync(id, ct) ?? throw new ArticleNotFoundException(id.ToString());
        article.MarkHelpful();
        await repository.SaveChangesAsync(ct);
    }

    public async Task MarkNotHelpfulAsync(Guid id, CancellationToken ct)
    {
        var article = await repository.GetByIdAsync(id, ct) ?? throw new ArticleNotFoundException(id.ToString());
        article.MarkNotHelpful();
        await repository.SaveChangesAsync(ct);
    }

    internal static ArticleDto ToDto(Article a) => new(a.Id, a.KbCategoryId, a.TitleEn, a.TitleAr, a.BodyEn, a.BodyAr, a.Status, a.AuthorName, a.LastUpdatedByName, a.LastUpdatedAtUtc, a.ViewCount, a.HelpfulCount, a.NotHelpfulCount);
}
