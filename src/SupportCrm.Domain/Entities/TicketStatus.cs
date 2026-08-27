namespace SupportCrm.Domain.Entities;

// Full vocabulary per the intake (New/Open/Pending/Resolved/Closed). TM-1 only sets it
// to New on creation; TM-4 builds the public transition/escalation actions.
public enum TicketStatus
{
    New,
    Open,
    Pending,
    Resolved,
    Closed
}
