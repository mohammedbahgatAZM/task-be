namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IContentVersionRepository
{
    Task<IReadOnlyList<ContentVersionEntry>> GetForContentAsync(string contentType, Guid contentId, CancellationToken ct);
    Task<int> GetNextVersionNumberAsync(string contentType, Guid contentId, CancellationToken ct);
    Task AddAsync(ContentVersionEntry entry, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
