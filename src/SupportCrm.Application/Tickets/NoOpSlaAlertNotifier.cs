namespace SupportCrm.Application.Tickets;

public class NoOpSlaAlertNotifier : ISlaAlertNotifier
{
    public Task NotifyWarningAsync(Guid agentId, Guid ticketId, string referenceNumber, CancellationToken ct) => Task.CompletedTask;
    public Task NotifyBreachAsync(Guid agentId, Guid ticketId, string referenceNumber, CancellationToken ct) => Task.CompletedTask;
    public Task SendDigestAsync(Guid agentId, IReadOnlyList<AtRiskTicketDto> atRiskTickets, CancellationToken ct) => Task.CompletedTask;
}
