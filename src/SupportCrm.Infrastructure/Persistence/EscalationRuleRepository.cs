namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class EscalationRuleRepository(SupportCrmDbContext dbContext) : IEscalationRuleRepository
{
    public async Task<IReadOnlyList<EscalationRule>> GetActiveOrderedAsync(CancellationToken ct) =>
        await dbContext.EscalationRules.Where(r => r.IsActive).OrderBy(r => r.SortOrder).ToListAsync(ct);

    public async Task<IReadOnlyList<EscalationTier>> GetTiersAsync(Guid escalationRuleId, CancellationToken ct) =>
        await dbContext.EscalationTiers.Where(t => t.EscalationRuleId == escalationRuleId).ToListAsync(ct);

    public Task AddAsync(EscalationRule rule, CancellationToken ct)
    {
        dbContext.EscalationRules.Add(rule);
        return Task.CompletedTask;
    }

    public Task AddTierAsync(EscalationTier tier, CancellationToken ct)
    {
        dbContext.EscalationTiers.Add(tier);
        return Task.CompletedTask;
    }

    public Task<bool> HasFiredAsync(Guid ticketId, Guid escalationRuleId, int tierNumber, CancellationToken ct) =>
        dbContext.EscalationLogEntries.AnyAsync(e => e.TicketId == ticketId && e.EscalationRuleId == escalationRuleId && e.TierNumber == tierNumber, ct);

    public Task AddLogEntryAsync(EscalationLogEntry entry, CancellationToken ct)
    {
        dbContext.EscalationLogEntries.Add(entry);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<EscalationLogEntry>> GetLogForTicketAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.EscalationLogEntries.Where(e => e.TicketId == ticketId).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
