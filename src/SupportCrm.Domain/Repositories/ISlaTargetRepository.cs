namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ISlaTargetRepository
{
    Task<IReadOnlyList<SlaTarget>> GetActiveAsync(CancellationToken ct);
    Task AddAsync(SlaTarget target, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
