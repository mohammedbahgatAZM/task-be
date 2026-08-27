namespace SupportCrm.Domain.Entities;

// Belongs to one EscalationRule. TierNumber is the firing order within that rule (1, 2, 3...);
// each tier fires at most once per ticket (see EscalationLogEntry / IEscalationRuleRepository.HasFiredAsync).
public class EscalationTier
{
    public Guid Id { get; private set; }
    public Guid EscalationRuleId { get; private set; }
    public int TierNumber { get; private set; }
    public int TriggerPercentage { get; private set; } // 1–100+ of resolution time-to-breach elapsed
    public Guid? ReassignToAgentId { get; private set; }
    public Guid? ReassignToTeamId { get; private set; }
    public TicketPriority? RaisePriorityTo { get; private set; }
    public bool NotifySupervisor { get; private set; }

    private EscalationTier() { } // EF Core

    public EscalationTier(Guid escalationRuleId, int tierNumber, int triggerPercentage, Guid? reassignToAgentId, Guid? reassignToTeamId, TicketPriority? raisePriorityTo, bool notifySupervisor)
    {
        if (triggerPercentage <= 0)
            throw new ArgumentException("Trigger percentage must be positive.", nameof(triggerPercentage));
        if (reassignToAgentId is not null && reassignToTeamId is not null)
            throw new ArgumentException("A tier can reassign to an agent or a team, not both.", nameof(reassignToAgentId));
        if (reassignToAgentId is null && reassignToTeamId is null && raisePriorityTo is null && !notifySupervisor)
            throw new ArgumentException("A tier must configure at least one action (reassign, raise priority, or notify supervisor).", nameof(notifySupervisor));

        Id = Guid.NewGuid();
        EscalationRuleId = escalationRuleId;
        TierNumber = tierNumber;
        TriggerPercentage = triggerPercentage;
        ReassignToAgentId = reassignToAgentId;
        ReassignToTeamId = reassignToTeamId;
        RaisePriorityTo = raisePriorityTo;
        NotifySupervisor = notifySupervisor;
    }
}
