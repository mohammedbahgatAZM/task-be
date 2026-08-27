namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ISearchLogRepository
{
    Task AddAsync(SearchLog entry, CancellationToken ct);
    Task<IReadOnlyList<SearchLog>> GetZeroResultLogsAsync(int take, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
