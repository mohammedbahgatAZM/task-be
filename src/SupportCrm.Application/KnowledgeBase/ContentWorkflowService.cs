namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ContentWorkflowService(
    IArticleRepository articleRepository,
    IGuideRepository guideRepository,
    IContentVersionRepository versionRepository,
    IAgentRepository agentRepository,
    TimeProvider timeProvider)
{
    public async Task SubmitForReviewAsync(string contentType, Guid contentId, TransitionContentRequest request, CancellationToken ct)
    {
        await RequireEditorAsync(request.EditorAgentId, ct);
        if (contentType == "Article")
        {
            var article = await articleRepository.GetByIdAsync(contentId, ct) ?? throw new ArticleNotFoundException(contentId.ToString());
            article.SubmitForReview();
            await articleRepository.SaveChangesAsync(ct);
        }
        else
        {
            var guide = await guideRepository.GetByIdAsync(contentId, ct) ?? throw new GuideNotFoundException(contentId.ToString());
            guide.SubmitForReview();
            await guideRepository.SaveChangesAsync(ct);
        }
    }

    public async Task PublishAsync(string contentType, Guid contentId, PublishContentRequest request, CancellationToken ct)
    {
        await RequireEditorAsync(request.EditorAgentId, ct);
        if (contentType == "Article")
        {
            var article = await articleRepository.GetByIdAsync(contentId, ct) ?? throw new ArticleNotFoundException(contentId.ToString());
            article.Publish(request.ReviewDueAtUtc);
            await articleRepository.SaveChangesAsync(ct);
        }
        else
        {
            var guide = await guideRepository.GetByIdAsync(contentId, ct) ?? throw new GuideNotFoundException(contentId.ToString());
            guide.Publish(request.ReviewDueAtUtc);
            await guideRepository.SaveChangesAsync(ct);
        }
    }

    public Task UnpublishAsync(string contentType, Guid contentId, TransitionContentRequest request, CancellationToken ct) =>
        ApplyTransitionAsync(contentType, contentId, request.EditorAgentId, a => a.Unpublish(), g => g.Unpublish(), ct);

    public Task ArchiveAsync(string contentType, Guid contentId, TransitionContentRequest request, CancellationToken ct) =>
        ApplyTransitionAsync(contentType, contentId, request.EditorAgentId, a => a.Archive(), g => g.Archive(), ct);

    private async Task ApplyTransitionAsync(string contentType, Guid contentId, Guid editorAgentId, Action<Article> onArticle, Action<Guide> onGuide, CancellationToken ct)
    {
        await RequireEditorAsync(editorAgentId, ct);
        if (contentType == "Article")
        {
            var article = await articleRepository.GetByIdAsync(contentId, ct) ?? throw new ArticleNotFoundException(contentId.ToString());
            onArticle(article);
            await articleRepository.SaveChangesAsync(ct);
        }
        else
        {
            var guide = await guideRepository.GetByIdAsync(contentId, ct) ?? throw new GuideNotFoundException(contentId.ToString());
            onGuide(guide);
            await guideRepository.SaveChangesAsync(ct);
        }
    }

    // Snapshots the CURRENT (pre-edit) state if the content has ever been published, then
    // applies the edit. Prefer this over ArticleService.UpdateAsync directly once versioning
    // matters — ArticleService.UpdateAsync still exists for never-published drafts.
    public async Task SnapshotIfPublishedThenUpdateArticleAsync(Guid articleId, UpdateArticleRequest request, CancellationToken ct)
    {
        var article = await articleRepository.GetByIdAsync(articleId, ct) ?? throw new ArticleNotFoundException(articleId.ToString());
        if (article.HasBeenPublished)
        {
            var versionNumber = await versionRepository.GetNextVersionNumberAsync("Article", articleId, ct);
            await versionRepository.AddAsync(new ContentVersionEntry("Article", articleId, versionNumber,
                article.TitleEn, article.TitleAr, article.BodyEn, article.BodyAr, request.ChangedBy, timeProvider.GetUtcNow()), ct);
            await versionRepository.SaveChangesAsync(ct);
        }
        article.RecordUpdate(request.TitleEn?.Trim(), request.TitleAr?.Trim(), request.BodyEn?.Trim(), request.BodyAr?.Trim(), request.ChangedBy, timeProvider.GetUtcNow());
        await articleRepository.SaveChangesAsync(ct);
    }

    // Generic snapshot-only variant for Guide — GuideService.UpdateAsync already owns the editor
    // check and the actual field update; call this immediately before it, in the same request.
    public async Task SnapshotIfPublishedAsync(string contentType, Guid contentId, CancellationToken ct)
    {
        if (contentType == "Article")
        {
            var article = await articleRepository.GetByIdAsync(contentId, ct);
            if (article is { HasBeenPublished: true })
            {
                var versionNumber = await versionRepository.GetNextVersionNumberAsync("Article", contentId, ct);
                await versionRepository.AddAsync(new ContentVersionEntry("Article", contentId, versionNumber,
                    article.TitleEn, article.TitleAr, article.BodyEn, article.BodyAr, article.LastUpdatedByName, timeProvider.GetUtcNow()), ct);
                await versionRepository.SaveChangesAsync(ct);
            }
        }
        else
        {
            var guide = await guideRepository.GetByIdAsync(contentId, ct);
            if (guide is { HasBeenPublished: true })
            {
                var versionNumber = await versionRepository.GetNextVersionNumberAsync("Guide", contentId, ct);
                await versionRepository.AddAsync(new ContentVersionEntry("Guide", contentId, versionNumber,
                    guide.TitleEn, guide.TitleAr, guide.BodyEn, guide.BodyAr, guide.LastUpdatedByName, timeProvider.GetUtcNow()), ct);
                await versionRepository.SaveChangesAsync(ct);
            }
        }
    }

    public async Task<IReadOnlyList<ContentVersionDto>> GetVersionHistoryAsync(string contentType, Guid contentId, CancellationToken ct) =>
        (await versionRepository.GetForContentAsync(contentType, contentId, ct))
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new ContentVersionDto(v.VersionNumber, v.TitleEnSnapshot, v.TitleArSnapshot, v.BodyEnSnapshot, v.BodyArSnapshot, v.ChangedBy, v.ChangedAtUtc))
            .ToList();

    public async Task<IReadOnlyList<DueForReviewItemDto>> GetDueForReviewAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var articles = (await articleRepository.GetDueForReviewAsync(now, ct))
            .Select(a => new DueForReviewItemDto("Article", a.Id, a.TitleEn, a.TitleAr, a.ReviewDueAtUtc!.Value));
        var guides = (await guideRepository.GetDueForReviewAsync(now, ct))
            .Select(g => new DueForReviewItemDto("Guide", g.Id, g.TitleEn, g.TitleAr, g.ReviewDueAtUtc!.Value));
        return articles.Concat(guides).OrderBy(d => d.ReviewDueAtUtc).ToList();
    }

    private async Task RequireEditorAsync(Guid agentId, CancellationToken ct)
    {
        var agent = await agentRepository.GetByIdAsync(agentId, ct);
        if (agent is null || !agent.IsKnowledgeBaseEditor)
            throw new KbEditorRequiredException(agentId);
    }
}
