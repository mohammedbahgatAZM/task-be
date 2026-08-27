namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketMessageRepository(SupportCrmDbContext dbContext) : ITicketMessageRepository
{
    public async Task<IReadOnlyList<TicketMessage>> GetMessagesAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.TicketMessages.Where(m => m.TicketId == ticketId).ToListAsync(ct);

    public Task AddMessageAsync(TicketMessage message, CancellationToken ct)
    {
        dbContext.TicketMessages.Add(message);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TicketNote>> GetNotesAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.TicketNotes.Where(n => n.TicketId == ticketId).ToListAsync(ct);

    public Task AddNoteAsync(TicketNote note, CancellationToken ct)
    {
        dbContext.TicketNotes.Add(note);
        return Task.CompletedTask;
    }

    public Task<TicketMessage?> GetMessageByIdAsync(Guid messageId, CancellationToken ct) =>
        dbContext.TicketMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);

    public Task AddDeliveryStatusAsync(TicketMessageDeliveryStatus status, CancellationToken ct)
    {
        dbContext.TicketMessageDeliveryStatuses.Add(status);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TicketMessageDeliveryStatus>> GetDeliveryStatusesAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.TicketMessageDeliveryStatuses
            .Where(s => dbContext.TicketMessages.Any(m => m.Id == s.TicketMessageId && m.TicketId == ticketId))
            .ToListAsync(ct);

    public Task<int> CountByTicketAsync(Guid ticketId, CancellationToken ct) =>
        dbContext.TicketMessages.CountAsync(m => m.TicketId == ticketId, ct);

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetFirstAgentMessageTimesAsync(IReadOnlyList<Guid> ticketIds, CancellationToken ct) =>
        await dbContext.TicketMessages
            .Where(m => ticketIds.Contains(m.TicketId) && m.AuthorKind == "Agent")
            .GroupBy(m => m.TicketId)
            .Select(g => new { TicketId = g.Key, FirstAt = g.Min(m => m.CreatedAtUtc) })
            .ToDictionaryAsync(x => x.TicketId, x => x.FirstAt, ct);

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
