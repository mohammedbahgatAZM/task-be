namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AuditLogRepository(SupportCrmDbContext dbContext) : IAuditLogRepository
{
    public Task AddAsync(AuditLogEntry entry, CancellationToken ct) { dbContext.AuditLogEntries.Add(entry); return Task.CompletedTask; }
    public async Task<IReadOnlyList<AuditLogEntry>> GetAllAsync(CancellationToken ct) => await dbContext.AuditLogEntries.ToListAsync(ct);
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
