namespace SupportCrm.Domain.Entities;

public class TicketAssignmentChangeEntry
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid? OldAgentId { get; private set; }
    public Guid? NewAgentId { get; private set; }
    public Guid? OldTeamId { get; private set; }
    public Guid? NewTeamId { get; private set; }
    public string ChangedBy { get; private set; } = default!;
    public DateTimeOffset ChangedAtUtc { get; private set; }

    private TicketAssignmentChangeEntry() { } // EF Core

    public TicketAssignmentChangeEntry(Guid ticketId, Guid? oldAgentId, Guid? newAgentId, Guid? oldTeamId, Guid? newTeamId, string changedBy, DateTimeOffset changedAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        OldAgentId = oldAgentId;
        NewAgentId = newAgentId;
        OldTeamId = oldTeamId;
        NewTeamId = newTeamId;
        ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "unknown" : changedBy;
        ChangedAtUtc = changedAtUtc;
    }
}
