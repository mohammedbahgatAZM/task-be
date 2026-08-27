namespace SupportCrm.Application.Tickets;

/// <summary>
/// Notifies the previous and new assignee of a reassignment. No real notification
/// channel (email/push/SMS) exists in this codebase yet — register
/// <see cref="NoOpAssignmentNotifier"/> until one does.
/// </summary>
public interface IAssignmentNotifier
{
    Task NotifyReassignedAsync(Guid ticketId, Guid? previousAgentId, Guid? previousTeamId, Guid? newAgentId, Guid? newTeamId, CancellationToken ct);
}
