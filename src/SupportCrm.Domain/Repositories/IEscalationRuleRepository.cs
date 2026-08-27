namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IEscalationRuleRepository
{
    Task<IReadOnlyList<EscalationRule>> GetActiveOrderedAsync(CancellationToken ct);
    Task<IReadOnlyList<EscalationTier>> GetTiersAsync(Guid escalationRuleId, CancellationToken ct);
    Task AddAsync(EscalationRule rule, CancellationToken ct);
    Task AddTierAsync(EscalationTier tier, CancellationToken ct);
    Task<bool> HasFiredAsync(Guid ticketId, Guid escalationRuleId, int tierNumber, CancellationToken ct);
    Task AddLogEntryAsync(EscalationLogEntry entry, CancellationToken ct);
    Task<IReadOnlyList<EscalationLogEntry>> GetLogForTicketAsync(Guid ticketId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
