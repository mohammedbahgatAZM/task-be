namespace SupportCrm.Application.Tickets;

public record TicketCollaboratorDto(Guid Id, Guid TicketId, Guid AgentId, DateTimeOffset AddedAtUtc);
public record AddTicketCollaboratorRequest(Guid AgentId);
