namespace SupportCrm.Application.Integrations;

using SupportCrm.Domain.Entities;

// INT-1 — "a documented REST API is available covering core objects (customers, tickets,
// users)." A deliberately small, stable contract of its own — decoupled from the richer internal
// DTOs (CustomerDto/TicketDto/AgentDto) so an internal refactor never breaks an external
// integration built against this shape.
public record ExternalCustomerDto(Guid Id, string CustomerNumber, string Name, string? Company, DateTimeOffset CreatedAtUtc);
public record ExternalCreateCustomerRequest(string Name, string? Company);

public record ExternalTicketDto(Guid Id, string ReferenceNumber, Guid CustomerId, TicketChannel Channel, string Subject, TicketStatus Status, TicketPriority Priority, DateTimeOffset CreatedAtUtc);
public record ExternalCreateTicketRequest(Guid? CustomerId, string RequesterName, string? RequesterContactValue, string Subject, string? Description);

public record ExternalUserDto(Guid Id, string Name, bool IsAvailable);
