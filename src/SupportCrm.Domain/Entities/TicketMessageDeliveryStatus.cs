namespace SupportCrm.Domain.Entities;

public class TicketMessageDeliveryStatus
{
    public Guid Id { get; private set; }
    public Guid TicketMessageId { get; private set; }
    public string Status { get; private set; } = default!; // "Sent" | "Delivered" | "Read" | "Bounced" | "Failed"
    public string? Detail { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }

    private TicketMessageDeliveryStatus() { } // EF Core

    public TicketMessageDeliveryStatus(Guid ticketMessageId, string status, string? detail, DateTimeOffset occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status is required.", nameof(status));

        Id = Guid.NewGuid();
        TicketMessageId = ticketMessageId;
        Status = status;
        Detail = detail;
        OccurredAtUtc = occurredAtUtc;
    }
}
