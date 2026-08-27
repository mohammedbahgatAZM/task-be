namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

/// <summary>
/// Notifies a ticket's customer that its status changed, when the caller opts in via
/// `NotifyCustomer` on the status-change request. No real notification channel exists
/// in this codebase yet — register <see cref="NoOpCustomerStatusNotifier"/> until one does.
/// </summary>
public interface ICustomerStatusNotifier
{
    Task NotifyStatusChangedAsync(Guid ticketId, TicketStatus newStatus, CancellationToken ct);
}
