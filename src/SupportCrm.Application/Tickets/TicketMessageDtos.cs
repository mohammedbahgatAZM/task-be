namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record AddTicketMessageRequest(string Body, string AuthorName, string AuthorKind);
public record TicketMessageDto(Guid Id, string Body, string AuthorName, string AuthorKind, DateTimeOffset CreatedAtUtc);
public record AddTicketNoteRequest(string Text, string AuthorName);
public record TicketNoteDto(Guid Id, string Text, string AuthorName, DateTimeOffset CreatedAtUtc);

public record TicketTimelineEntryDto(
    Guid Id,
    string Kind,          // "Message" | "Note" | "StatusChange" | "Assignment" | "Escalation"
    bool IsCustomerVisible,
    DateTimeOffset OccurredAtUtc,
    string Summary,
    string AuthorName,
    TicketChannel? Channel,
    string? AuthorKind);   // "Customer" | "Agent" | "System" for Message entries; null otherwise — lets
                           // the frontend mirror ChannelReplyDispatcher's "last customer-authored channel"
                           // logic exactly instead of approximating from Kind/IsCustomerVisible alone.
