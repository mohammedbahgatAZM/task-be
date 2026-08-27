namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Ticket?> GetByReferenceNumberAsync(string referenceNumber, CancellationToken ct);
    Task<IReadOnlyList<Ticket>> GetByCustomerAsync(Guid customerId, CancellationToken ct);
    Task<int> CountOpenByCustomerAsync(Guid customerId, CancellationToken ct);
    Task AddAsync(Ticket ticket, CancellationToken ct);
    Task<IReadOnlyList<TicketStatusChangeEntry>> GetStatusHistoryAsync(Guid ticketId, CancellationToken ct);
    Task AddStatusChangeAsync(TicketStatusChangeEntry entry, CancellationToken ct);
    Task<IReadOnlyList<TicketFieldChangeEntry>> GetFieldChangeLogAsync(Guid ticketId, CancellationToken ct);
    Task AddFieldChangeAsync(TicketFieldChangeEntry entry, CancellationToken ct);
    // Keyed by category id as a string ("Uncategorized" for tickets with no category) rather than
    // Guid? — Dictionary<Guid?, int> throws ArgumentNullException at runtime when inserting the
    // uncategorized group's null key (Nullable<T> as TKey is only a compile-time nullability
    // warning, not a safe no-op; the runtime null-check fires regardless of how the dictionary is
    // built). The only caller converts to string keys immediately anyway, so do it here instead.
    Task<IReadOnlyDictionary<string, int>> CountGroupedByCategoryAsync(CancellationToken ct);
    Task<IReadOnlyDictionary<TicketPriority, int>> CountGroupedByPriorityAsync(CancellationToken ct);
    Task<IReadOnlyList<Ticket>> GetUnassignedAsync(CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, int>> CountOpenGroupedByAgentAsync(CancellationToken ct);
    Task AddAssignmentChangeAsync(TicketAssignmentChangeEntry entry, CancellationToken ct);
    Task<IReadOnlyList<TicketAssignmentChangeEntry>> GetAssignmentHistoryAsync(Guid ticketId, CancellationToken ct);
    Task AddEscalationAsync(TicketEscalationEntry entry, CancellationToken ct);
    Task<IReadOnlyList<TicketEscalationEntry>> GetEscalationsAsync(Guid ticketId, CancellationToken ct);
    Task<Ticket?> FindOpenTicketForCustomerAsync(Guid customerId, CancellationToken ct);
    Task<IReadOnlyList<Ticket>> GetAssignedToAgentAsync(Guid agentId, CancellationToken ct);
    Task<IReadOnlyList<Ticket>> GetOpenAsync(CancellationToken ct);
    // Reports & Management — loads every ticket for in-memory filtering/aggregation. Acceptable
    // at this app's demo scale, same standard as other flagged in-memory-filter/N+1 notes.
    Task<IReadOnlyList<Ticket>> GetAllAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
