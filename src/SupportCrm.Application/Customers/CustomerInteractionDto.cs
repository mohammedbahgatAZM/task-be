namespace SupportCrm.Application.Customers;

public record CustomerInteractionDto(
    Guid Id,
    string Channel,        // free-form discriminator, e.g. "Note", "Ticket", "Call", "Chat", "Email" — sources define their own values
    DateTimeOffset OccurredAtUtc,
    string Summary,
    string? AgentName,
    string? SourceUrl);    // relative link to the original record; null when no UI exists yet for that source
