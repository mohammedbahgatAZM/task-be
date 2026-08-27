namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IGuideRepository
{
    Task<Guide?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Guide>> GetAllAsync(bool includeUnpublished, CancellationToken ct);
    Task<IReadOnlyList<Guide>> GetByTicketCategoryAsync(Guid ticketCategoryId, bool includeUnpublished, CancellationToken ct);
    Task<IReadOnlyList<Guide>> SearchPublishedAsync(string query, CancellationToken ct);
    Task<IReadOnlyList<Guide>> GetDueForReviewAsync(DateTimeOffset asOfUtc, CancellationToken ct);
    Task AddAsync(Guide guide, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetLinkedTicketCategoryIdsAsync(Guid guideId, CancellationToken ct);
    Task AddCategoryLinkAsync(GuideTicketCategory link, CancellationToken ct);
    Task RemoveCategoryLinkAsync(Guid guideId, Guid ticketCategoryId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
