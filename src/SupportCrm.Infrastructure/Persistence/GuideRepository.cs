namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class GuideRepository(SupportCrmDbContext dbContext) : IGuideRepository
{
    public Task<Guide?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.Guides.FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task<IReadOnlyList<Guide>> GetAllAsync(bool includeUnpublished, CancellationToken ct) =>
        await Filter(dbContext.Guides, includeUnpublished).ToListAsync(ct);

    public async Task<IReadOnlyList<Guide>> GetByTicketCategoryAsync(Guid ticketCategoryId, bool includeUnpublished, CancellationToken ct)
    {
        var guideIds = dbContext.GuideTicketCategories.Where(l => l.TicketCategoryId == ticketCategoryId).Select(l => l.GuideId);
        var query = dbContext.Guides.Where(g => guideIds.Contains(g.Id));
        return await Filter(query, includeUnpublished).ToListAsync(ct);
    }

    private static IQueryable<Guide> Filter(IQueryable<Guide> query, bool includeUnpublished) =>
        includeUnpublished ? query : query.Where(g => g.Status == KbContentStatus.Published);

    public async Task<IReadOnlyList<Guide>> SearchPublishedAsync(string query, CancellationToken ct) =>
        await dbContext.Guides
            .Where(g => g.Status == KbContentStatus.Published)
            .Where(g =>
                EF.Functions.ILike(g.TitleEn ?? "", $"%{query}%") || EF.Functions.ILike(g.TitleAr ?? "", $"%{query}%") ||
                EF.Functions.ILike(g.BodyEn ?? "", $"%{query}%") || EF.Functions.ILike(g.BodyAr ?? "", $"%{query}%"))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guide>> GetDueForReviewAsync(DateTimeOffset asOfUtc, CancellationToken ct) =>
        await dbContext.Guides.Where(g => g.ReviewDueAtUtc != null && g.ReviewDueAtUtc <= asOfUtc).ToListAsync(ct);

    public Task AddAsync(Guide guide, CancellationToken ct)
    {
        dbContext.Guides.Add(guide);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Guid>> GetLinkedTicketCategoryIdsAsync(Guid guideId, CancellationToken ct) =>
        await dbContext.GuideTicketCategories.Where(l => l.GuideId == guideId).Select(l => l.TicketCategoryId).ToListAsync(ct);

    public Task AddCategoryLinkAsync(GuideTicketCategory link, CancellationToken ct)
    {
        dbContext.GuideTicketCategories.Add(link);
        return Task.CompletedTask;
    }

    public async Task RemoveCategoryLinkAsync(Guid guideId, Guid ticketCategoryId, CancellationToken ct)
    {
        var link = await dbContext.GuideTicketCategories.FirstOrDefaultAsync(l => l.GuideId == guideId && l.TicketCategoryId == ticketCategoryId, ct);
        if (link is not null) dbContext.GuideTicketCategories.Remove(link);
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
