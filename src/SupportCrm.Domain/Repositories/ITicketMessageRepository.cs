namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketMessageRepository
{
    Task<IReadOnlyList<TicketMessage>> GetMessagesAsync(Guid ticketId, CancellationToken ct);
    Task AddMessageAsync(TicketMessage message, CancellationToken ct);
    Task<IReadOnlyList<TicketNote>> GetNotesAsync(Guid ticketId, CancellationToken ct);
    Task AddNoteAsync(TicketNote note, CancellationToken ct);
    Task<TicketMessage?> GetMessageByIdAsync(Guid messageId, CancellationToken ct);
    Task AddDeliveryStatusAsync(TicketMessageDeliveryStatus status, CancellationToken ct);
    Task<IReadOnlyList<TicketMessageDeliveryStatus>> GetDeliveryStatusesAsync(Guid ticketId, CancellationToken ct);
    Task<int> CountByTicketAsync(Guid ticketId, CancellationToken ct);
    // Reports & Management — one grouped query for every ticket in a report, not a per-ticket round trip.
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetFirstAgentMessageTimesAsync(IReadOnlyList<Guid> ticketIds, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
