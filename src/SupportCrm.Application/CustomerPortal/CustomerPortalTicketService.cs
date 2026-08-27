namespace SupportCrm.Application.CustomerPortal;

using Microsoft.Extensions.Options;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;

public class CustomerPortalTicketService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    TicketService ticketService,
    IOptions<CustomerPortalOptions> options,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<CustomerTicketSummaryDto>> GetTicketsForCustomerAsync(Guid customerId, CustomerTicketListQuery query, CancellationToken ct)
    {
        var tickets = await ticketRepository.GetByCustomerAsync(customerId, ct);

        IEnumerable<Ticket> filtered = tickets;
        if (query.Status is not null) filtered = filtered.Where(t => t.Status == query.Status);
        if (query.CategoryId is not null) filtered = filtered.Where(t => t.CategoryId == query.CategoryId);
        if (query.From is not null) filtered = filtered.Where(t => t.CreatedAtUtc >= query.From);
        if (query.To is not null) filtered = filtered.Where(t => t.CreatedAtUtc <= query.To);
        if (!string.IsNullOrWhiteSpace(query.Query))
            filtered = filtered.Where(t =>
                t.Subject.Contains(query.Query, StringComparison.OrdinalIgnoreCase) ||
                (t.Description ?? "").Contains(query.Query, StringComparison.OrdinalIgnoreCase));

        var results = new List<CustomerTicketSummaryDto>();
        foreach (var ticket in filtered)
        {
            var history = await ticketRepository.GetStatusHistoryAsync(ticket.Id, ct);
            var lastUpdated = history.Count > 0 ? history.Max(h => h.ChangedAtUtc) : ticket.CreatedAtUtc;
            results.Add(new CustomerTicketSummaryDto(ticket.Id, ticket.ReferenceNumber, ticket.Subject, ticket.Status, ticket.Priority, ticket.CategoryId, ticket.CreatedAtUtc, lastUpdated));
        }

        return results.OrderByDescending(r => r.LastUpdatedAtUtc).ToList();
    }

    public async Task<TicketMessageDto> AddPortalReplyAsync(Guid ticketId, AddPortalReplyRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        if (ticket.CustomerId != request.CustomerId)
            throw new TicketOwnershipException(ticketId);

        var message = new TicketMessage(ticketId, request.Body.Trim(), request.CustomerName.Trim(), "Customer", timeProvider.GetUtcNow());
        message.SetChannel(TicketChannel.Portal);
        await messageRepository.AddMessageAsync(message, ct);
        await messageRepository.SaveChangesAsync(ct);

        return new TicketMessageDto(message.Id, message.Body, message.AuthorName, message.AuthorKind, message.CreatedAtUtc);
    }

    public async Task ReopenAsync(Guid ticketId, ReopenTicketRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        if (ticket.CustomerId != request.CustomerId)
            throw new TicketOwnershipException(ticketId);
        if (ticket.Status is not (TicketStatus.Resolved or TicketStatus.Closed))
            throw new InvalidOperationException("Only resolved or closed tickets can be reopened.");

        var history = await ticketRepository.GetStatusHistoryAsync(ticketId, ct);
        var lastResolvedOrClosedAt = history
            .Where(h => h.NewStatus is TicketStatus.Resolved or TicketStatus.Closed)
            .OrderByDescending(h => h.ChangedAtUtc)
            .Select(h => h.ChangedAtUtc)
            .First(); // guaranteed to exist — the ticket IS currently Resolved/Closed, so at least one such entry was written to get here

        var windowEnd = lastResolvedOrClosedAt.AddDays(options.Value.ReopenWindowDays);
        if (timeProvider.GetUtcNow() > windowEnd)
            throw new InvalidOperationException($"This ticket can no longer be reopened — the {options.Value.ReopenWindowDays}-day window has passed.");

        // changedByKind is "System", not "Customer" — TicketStatusChangeEntry's constructor only
        // accepts "Agent"/"System" and silently coerces anything else to "Agent", which would
        // misattribute this. Flagged as a stand-in until that entity gains a real "Customer" kind.
        await ticketService.RecordStatusChangeAsync(ticketId, TicketStatus.Open, request.CustomerName, "System", "Reopened by customer via self-service portal", ct);
    }

    // Design note: GetTicketsForCustomerAsync loads status history per ticket (N+1). Acceptable
    // at this app's per-customer scale (a customer has few tickets), flagged not silently ignored.
}
