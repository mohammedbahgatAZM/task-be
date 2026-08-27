namespace SupportCrm.Application.Tickets;

public class NoOpAssignmentNotifier : IAssignmentNotifier
{
    public Task NotifyReassignedAsync(Guid ticketId, Guid? previousAgentId, Guid? previousTeamId, Guid? newAgentId, Guid? newTeamId, CancellationToken ct)
        => Task.CompletedTask;
}
