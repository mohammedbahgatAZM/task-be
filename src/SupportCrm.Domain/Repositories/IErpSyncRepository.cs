namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IErpSyncRepository
{
    Task<ErpSyncState?> GetStateAsync(Guid customerId, CancellationToken ct);
    Task UpsertStateAsync(ErpSyncState state, CancellationToken ct);
    Task AddLogAsync(ErpSyncLog log, CancellationToken ct);
    Task<IReadOnlyList<ErpSyncLog>> GetLogsAsync(Guid? customerId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
