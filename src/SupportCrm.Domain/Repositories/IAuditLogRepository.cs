namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken ct);
    Task<IReadOnlyList<AuditLogEntry>> GetAllAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
