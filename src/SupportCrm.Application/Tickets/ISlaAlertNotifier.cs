namespace SupportCrm.Application.Tickets;

/// <summary>
/// Delivers SLA warning/breach alerts and digests over email/push. No real email/push channel
/// exists in this codebase yet — register <see cref="NoOpSlaAlertNotifier"/> until one does,
/// following the same seam pattern as IAssignmentNotifier and ICustomerStatusNotifier. In-app
/// delivery does not go through this seam — it's real, via AgentNotificationService, called
/// directly by SlaAlertService.
/// </summary>
public interface ISlaAlertNotifier
{
    Task NotifyWarningAsync(Guid agentId, Guid ticketId, string referenceNumber, CancellationToken ct);
    Task NotifyBreachAsync(Guid agentId, Guid ticketId, string referenceNumber, CancellationToken ct);
    Task SendDigestAsync(Guid agentId, IReadOnlyList<AtRiskTicketDto> atRiskTickets, CancellationToken ct);
}
