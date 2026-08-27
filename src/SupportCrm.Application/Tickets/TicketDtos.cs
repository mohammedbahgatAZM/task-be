namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record CreateTicketRequest(
    TicketChannel Channel,
    string Subject,
    string? Description,
    string RequesterName,
    string? RequesterContactValue,
    string CreatedBy,
    string? Language = null,
    Guid? CustomerId = null,
    Guid? CategoryId = null);

public record TicketDto(
    Guid Id,
    string ReferenceNumber,
    Guid CustomerId,
    TicketChannel Channel,
    string Subject,
    string? Description,
    TicketStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    Guid? CategoryId,
    TicketPriority Priority,
    Guid? DepartmentId = null);

public record TicketStatusViewDto(string ReferenceNumber, TicketStatus Status, DateTimeOffset LastUpdatedAtUtc);

public class TicketNotFoundException(string reference) : Exception($"Ticket '{reference}' was not found.");

public record SetCategoryRequest(Guid? CategoryId, string ChangedBy);
public record SetPriorityRequest(TicketPriority Priority, string ChangedBy);
public record TicketFieldChangeDto(Guid Id, string FieldName, string? OldValue, string? NewValue, string ChangedBy, DateTimeOffset ChangedAtUtc);
public record TicketGroupedCountsDto(IReadOnlyDictionary<string, int> ByCategory, IReadOnlyDictionary<string, int> ByPriority);

public record SetTicketStatusRequest(TicketStatus NewStatus, string ChangedBy, string? Reason, bool NotifyCustomer);
public record EscalateTicketRequest(Guid? EscalateToAgentId, Guid? EscalateToTeamId, string Reason, string ChangedBy);
public record TicketEscalationDto(Guid Id, Guid? EscalatedToAgentId, Guid? EscalatedToTeamId, string Reason, string EscalatedBy, DateTimeOffset EscalatedAtUtc);
