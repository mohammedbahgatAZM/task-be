namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record CreateEscalationRuleRequest(string Name, int SortOrder, Guid? CategoryId, TicketPriority? Priority);
public record EscalationRuleDto(Guid Id, string Name, int SortOrder, Guid? CategoryId, TicketPriority? Priority);
public record CreateEscalationTierRequest(int TierNumber, int TriggerPercentage, Guid? ReassignToAgentId, Guid? ReassignToTeamId, TicketPriority? RaisePriorityTo, bool NotifySupervisor);
public record EscalationTierDto(Guid Id, int TierNumber, int TriggerPercentage, Guid? ReassignToAgentId, Guid? ReassignToTeamId, TicketPriority? RaisePriorityTo, bool NotifySupervisor);
public record EscalationLogEntryDto(Guid Id, Guid EscalationRuleId, int TierNumber, string ActionSummary, DateTimeOffset TriggeredAtUtc);
