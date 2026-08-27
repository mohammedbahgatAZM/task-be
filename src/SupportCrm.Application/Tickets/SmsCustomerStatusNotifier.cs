namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SmsCustomerStatusNotifier(ITicketRepository ticketRepository, ISmsSender smsSender) : ICustomerStatusNotifier
{
    public async Task NotifyStatusChangedAsync(Guid ticketId, TicketStatus newStatus, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct);
        if (ticket?.RequesterContactValue is null) return; // no phone number on file — nothing to notify, not an error
        await smsSender.SendAsync(ticket.RequesterContactValue, $"Your ticket {ticket.ReferenceNumber} status is now {newStatus}.", ct);
    }
}
