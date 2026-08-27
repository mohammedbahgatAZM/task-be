namespace SupportCrm.Application.Sla;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;

public class SlaCalculationService(
    ITicketRepository ticketRepository,
    ICustomerRepository customerRepository,
    SlaTargetService targetService,
    BusinessCalendarService calendarService,
    TimeProvider timeProvider)
{
    public async Task<TicketSlaStatusDto?> GetStatusAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var customer = await customerRepository.GetByIdAsync(ticket.CustomerId, ct);
        return await ComputeAsync(ticket, customer?.Tier ?? CustomerTier.Standard, ct);
    }

    // Batch entry point for the dashboard — avoids re-resolving/re-fetching a ticket already
    // in hand. Skips tickets with no matching active SlaTarget (caller reports "NotApplicable").
    public async Task<IReadOnlyDictionary<Guid, TicketSlaStatusDto>> GetStatusesAsync(IReadOnlyList<Ticket> tickets, CancellationToken ct)
    {
        var result = new Dictionary<Guid, TicketSlaStatusDto>();
        foreach (var ticket in tickets)
        {
            var customer = await customerRepository.GetByIdAsync(ticket.CustomerId, ct);
            var status = await ComputeAsync(ticket, customer?.Tier ?? CustomerTier.Standard, ct);
            if (status is not null) result[ticket.Id] = status;
        }
        return result;
    }

    private async Task<TicketSlaStatusDto?> ComputeAsync(Ticket ticket, CustomerTier tier, CancellationToken ct)
    {
        var target = await targetService.ResolveAsync(ticket.Priority, ticket.CategoryId, tier, ct);
        if (target is null) return null; // no policy configured for this priority — caller reports "NotApplicable"

        var now = timeProvider.GetUtcNow();
        var baseResponseDueAtUtc = await calendarService.AddBusinessMinutesAsync(ticket.CreatedAtUtc, target.ResponseTargetMinutes, ct);
        var baseResolutionDueAtUtc = await calendarService.AddBusinessMinutesAsync(ticket.CreatedAtUtc, target.ResolutionTargetMinutes, ct);

        // Push both due-ats out by however long the ticket has spent Pending so far (business
        // time only) — the clock pauses while awaiting the customer, per the story's Pending-pause rule.
        var pausedMinutes = await GetPendingBusinessMinutesAsync(ticket.Id, now, ct);
        var responseDueAtUtc = pausedMinutes == 0 ? baseResponseDueAtUtc : await calendarService.AddBusinessMinutesAsync(baseResponseDueAtUtc, pausedMinutes, ct);
        var resolutionDueAtUtc = pausedMinutes == 0 ? baseResolutionDueAtUtc : await calendarService.AddBusinessMinutesAsync(baseResolutionDueAtUtc, pausedMinutes, ct);

        var isClosed = ticket.Status is TicketStatus.Closed or TicketStatus.Resolved;
        return new TicketSlaStatusDto(
            ticket.Id, target.Id, target.ResolutionTargetMinutes,
            responseDueAtUtc, resolutionDueAtUtc,
            IsResponseBreached: now >= responseDueAtUtc,
            IsResolutionBreached: !isClosed && now >= resolutionDueAtUtc,
            ResponseRemainingMinutes: Math.Max(0, (int)(responseDueAtUtc - now).TotalMinutes),
            ResolutionRemainingMinutes: Math.Max(0, (int)(resolutionDueAtUtc - now).TotalMinutes));
    }

    private async Task<int> GetPendingBusinessMinutesAsync(Guid ticketId, DateTimeOffset now, CancellationToken ct)
    {
        var history = (await ticketRepository.GetStatusHistoryAsync(ticketId, ct)).OrderBy(h => h.ChangedAtUtc).ToList();
        var total = 0;
        for (var i = 0; i < history.Count; i++)
        {
            if (history[i].NewStatus != TicketStatus.Pending) continue;
            var from = history[i].ChangedAtUtc;
            var to = i + 1 < history.Count ? history[i + 1].ChangedAtUtc : now;
            total += await calendarService.CalculateBusinessMinutesBetweenAsync(from, to, ct);
        }
        return total;
    }
}
