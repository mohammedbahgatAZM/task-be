namespace SupportCrm.Application.Tickets;

public record CreateTicketTaskRequest(string Note, DateTimeOffset DueAtUtc, Guid AssignedAgentId, string CreatedBy);
public record TicketTaskDto(Guid Id, Guid TicketId, string Note, DateTimeOffset DueAtUtc, Guid AssignedAgentId, bool IsCompleted, DateTimeOffset CreatedAtUtc);
public record ReassignTicketTaskRequest(Guid NewAgentId);
public record AgentNotificationDto(Guid Id, string Kind, string Message, Guid? RelatedTicketId, bool IsRead, DateTimeOffset CreatedAtUtc);
