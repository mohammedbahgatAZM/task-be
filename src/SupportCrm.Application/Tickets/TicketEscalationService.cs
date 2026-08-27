namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketEscalationService(
    ITicketRepository ticketRepository,
    TicketAssignmentService assignmentService,
    TimeProvider timeProvider)
{
    public async Task EscalateAsync(Guid ticketId, EscalateTicketRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var now = timeProvider.GetUtcNow();

        // Reuses TM-3's assignment write path — an escalation IS a reassignment, plus a reason.
        await assignmentService.AssignAsync(ticketId,
            new AssignTicketRequest(request.EscalateToAgentId, request.EscalateToTeamId, request.ChangedBy), ct);

        var entry = new TicketEscalationEntry(ticketId, request.EscalateToAgentId, request.EscalateToTeamId, request.Reason, request.ChangedBy, now);
        await ticketRepository.AddEscalationAsync(entry, ct);

        ticket.MarkEscalated(now);
        await ticketRepository.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TicketEscalationDto>> GetEscalationsAsync(Guid ticketId, CancellationToken ct) =>
        (await ticketRepository.GetEscalationsAsync(ticketId, ct))
            .OrderByDescending(e => e.EscalatedAtUtc)
            .Select(e => new TicketEscalationDto(e.Id, e.EscalatedToAgentId, e.EscalatedToTeamId, e.Reason, e.EscalatedBy, e.EscalatedAtUtc))
            .ToList();
}
