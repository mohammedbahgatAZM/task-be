namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketRepository(SupportCrmDbContext dbContext) : ITicketRepository
{
    private static readonly TicketStatus[] OpenStatuses = { TicketStatus.New, TicketStatus.Open, TicketStatus.Pending };

    public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Ticket?> GetByReferenceNumberAsync(string referenceNumber, CancellationToken ct) =>
        dbContext.Tickets.FirstOrDefaultAsync(t => t.ReferenceNumber == referenceNumber, ct);

    public async Task<IReadOnlyList<Ticket>> GetByCustomerAsync(Guid customerId, CancellationToken ct) =>
        await dbContext.Tickets.Where(t => t.CustomerId == customerId).ToListAsync(ct);

    public Task<int> CountOpenByCustomerAsync(Guid customerId, CancellationToken ct) =>
        dbContext.Tickets.CountAsync(t => t.CustomerId == customerId && OpenStatuses.Contains(t.Status), ct);

    public Task AddAsync(Ticket ticket, CancellationToken ct)
    {
        dbContext.Tickets.Add(ticket);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TicketStatusChangeEntry>> GetStatusHistoryAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.TicketStatusChangeEntries.Where(e => e.TicketId == ticketId).ToListAsync(ct);

    public Task AddStatusChangeAsync(TicketStatusChangeEntry entry, CancellationToken ct)
    {
        dbContext.TicketStatusChangeEntries.Add(entry);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TicketFieldChangeEntry>> GetFieldChangeLogAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.TicketFieldChangeEntries.Where(e => e.TicketId == ticketId).ToListAsync(ct);

    public Task AddFieldChangeAsync(TicketFieldChangeEntry entry, CancellationToken ct)
    {
        dbContext.TicketFieldChangeEntries.Add(entry);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Ticket>> GetAssignedToAgentAsync(Guid agentId, CancellationToken ct) =>
        await dbContext.Tickets.Where(t => t.AssignedAgentId == agentId).ToListAsync(ct);

    public async Task<IReadOnlyList<Ticket>> GetOpenAsync(CancellationToken ct) =>
        await dbContext.Tickets.Where(t => OpenStatuses.Contains(t.Status)).ToListAsync(ct);

    public async Task<IReadOnlyList<Ticket>> GetAllAsync(CancellationToken ct) =>
        await dbContext.Tickets.ToListAsync(ct);

    public async Task<IReadOnlyDictionary<string, int>> CountGroupedByCategoryAsync(CancellationToken ct)
    {
        // Group by the raw nullable CategoryId in the query (so uncategorized tickets still form
        // their own group), but convert to a string key *before* it ever reaches a Dictionary —
        // Dictionary<Guid?, int> throws ArgumentNullException inserting the null-key group at
        // runtime, regardless of whether it's built via ToDictionary or a manual foreach.
        var grouped = await dbContext.Tickets
            .GroupBy(t => t.CategoryId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var result = new Dictionary<string, int>();
        foreach (var g in grouped) result[g.Key?.ToString() ?? "Uncategorized"] = g.Count;
        return result;
    }

    public async Task<IReadOnlyDictionary<TicketPriority, int>> CountGroupedByPriorityAsync(CancellationToken ct) =>
        await dbContext.Tickets.GroupBy(t => t.Priority).ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

    public async Task<IReadOnlyList<Ticket>> GetUnassignedAsync(CancellationToken ct) =>
        await dbContext.Tickets.Where(t => t.AssignedAgentId == null && t.AssignedTeamId == null).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, int>> CountOpenGroupedByAgentAsync(CancellationToken ct) =>
        await dbContext.Tickets
            .Where(t => t.AssignedAgentId != null && OpenStatuses.Contains(t.Status))
            .GroupBy(t => t.AssignedAgentId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

    public Task AddAssignmentChangeAsync(TicketAssignmentChangeEntry entry, CancellationToken ct)
    {
        dbContext.TicketAssignmentChangeEntries.Add(entry);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TicketAssignmentChangeEntry>> GetAssignmentHistoryAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.TicketAssignmentChangeEntries.Where(e => e.TicketId == ticketId).ToListAsync(ct);

    public Task AddEscalationAsync(TicketEscalationEntry entry, CancellationToken ct)
    {
        dbContext.TicketEscalationEntries.Add(entry);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TicketEscalationEntry>> GetEscalationsAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.TicketEscalationEntries.Where(e => e.TicketId == ticketId).ToListAsync(ct);

    public Task<Ticket?> FindOpenTicketForCustomerAsync(Guid customerId, CancellationToken ct) =>
        dbContext.Tickets
            .Where(t => t.CustomerId == customerId && OpenStatuses.Contains(t.Status))
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
