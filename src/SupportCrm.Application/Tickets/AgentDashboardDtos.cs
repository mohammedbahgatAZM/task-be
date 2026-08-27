namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record AgentDashboardTicketDto(
    Guid Id,
    string ReferenceNumber,
    string Subject,
    TicketStatus Status,
    TicketPriority Priority,
    Guid? CategoryId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset SlaDueAtUtc,
    string SlaState); // "OnTrack" | "NearingBreach" | "Breached" | "NotApplicable"
