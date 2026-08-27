namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Repositories;

public class TicketTimelineService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    ITicketCollaboratorRepository collaboratorRepository,
    IAgentRepository agentRepository)
{
    public async Task<IReadOnlyList<TicketTimelineEntryDto>> GetTimelineAsync(Guid ticketId, CancellationToken ct)
    {
        _ = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());

        var messages = await messageRepository.GetMessagesAsync(ticketId, ct);
        var notes = await messageRepository.GetNotesAsync(ticketId, ct);
        var statusChanges = await ticketRepository.GetStatusHistoryAsync(ticketId, ct);
        var assignments = await ticketRepository.GetAssignmentHistoryAsync(ticketId, ct);
        var escalations = await ticketRepository.GetEscalationsAsync(ticketId, ct);
        var collaborators = await collaboratorRepository.GetByTicketAsync(ticketId, ct);
        var agentNames = (await agentRepository.GetAllAsync(ct)).ToDictionary(a => a.Id, a => a.Name);

        var entries = new List<TicketTimelineEntryDto>();
        entries.AddRange(messages.Select(m => new TicketTimelineEntryDto(m.Id, "Message", true, m.CreatedAtUtc, m.Body, m.AuthorName, m.Channel, m.AuthorKind)));
        entries.AddRange(notes.Select(n => new TicketTimelineEntryDto(n.Id, "Note", false, n.CreatedAtUtc, n.Text, n.AuthorName, null, null)));
        entries.AddRange(statusChanges.Select(s => new TicketTimelineEntryDto(s.Id, "StatusChange", false, s.ChangedAtUtc,
            s.OldStatus is null ? $"Created with status {s.NewStatus}" : $"Status changed from {s.OldStatus} to {s.NewStatus}", s.ChangedBy, null, null)));
        entries.AddRange(assignments.Select(a => new TicketTimelineEntryDto(a.Id, "Assignment", false, a.ChangedAtUtc,
            "Reassigned", a.ChangedBy, null, null)));
        entries.AddRange(escalations.Select(e => new TicketTimelineEntryDto(e.Id, "Escalation", false, e.EscalatedAtUtc,
            $"Escalated: {e.Reason}", e.EscalatedBy, null, null)));
        entries.AddRange(collaborators.Select(c => new TicketTimelineEntryDto(
            c.Id, "Collaboration", false, c.AddedAtUtc,
            $"{(agentNames.TryGetValue(c.AgentId, out var name) ? name : "Unknown agent")} added as a collaborator",
            "System", null, null)));

        // Chronological, oldest first — reads like a conversation, unlike Customer
        // Management's CM-3 timeline (newest first): a single ticket's history is read
        // start-to-end, a customer's cross-channel feed is scanned most-recent-first.
        return entries.OrderBy(e => e.OccurredAtUtc).ToList();
    }
}
