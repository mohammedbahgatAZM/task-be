namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketAssignmentService(
    ITicketRepository ticketRepository,
    IAgentRepository agentRepository,
    IAssignmentNotifier notifier,
    TimeProvider timeProvider)
{
    public async Task AssignAsync(Guid ticketId, AssignTicketRequest request, CancellationToken ct)
    {
        if (request.AgentId is not null && request.TeamId is not null)
            throw new ArgumentException("Assign to an agent or a team, not both.", nameof(request));

        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var previousAgentId = ticket.AssignedAgentId;
        var previousTeamId = ticket.AssignedTeamId;

        ticket.AssignTo(request.AgentId, request.TeamId);

        await ticketRepository.AddAssignmentChangeAsync(
            new TicketAssignmentChangeEntry(ticketId, previousAgentId, request.AgentId, previousTeamId, request.TeamId, request.ChangedBy, timeProvider.GetUtcNow()), ct);
        await ticketRepository.SaveChangesAsync(ct);

        await notifier.NotifyReassignedAsync(ticketId, previousAgentId, previousTeamId, request.AgentId, request.TeamId, ct);
    }

    public async Task<IReadOnlyList<AgentLoadDto>> GetAgentLoadAsync(CancellationToken ct)
    {
        var agents = await agentRepository.GetAllAsync(ct);
        var loadByAgent = await ticketRepository.CountOpenGroupedByAgentAsync(ct);

        return agents
            .Select(a => new AgentLoadDto(a.Id, a.Name, loadByAgent.GetValueOrDefault(a.Id, 0)))
            .ToList();
    }

    public async Task<IReadOnlyList<TicketDto>> GetUnassignedAsync(CancellationToken ct)
    {
        var tickets = await ticketRepository.GetUnassignedAsync(ct);
        return tickets.Select(t => new TicketDto(t.Id, t.ReferenceNumber, t.CustomerId, t.Channel, t.Subject, t.Description, t.Status, t.CreatedAtUtc, t.ClosedAtUtc, t.CategoryId, t.Priority, t.DepartmentId)).ToList();
    }
}
