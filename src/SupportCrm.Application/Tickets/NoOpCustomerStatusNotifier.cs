namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public class NoOpCustomerStatusNotifier : ICustomerStatusNotifier
{
    public Task NotifyStatusChangedAsync(Guid ticketId, TicketStatus newStatus, CancellationToken ct) => Task.CompletedTask;
}
