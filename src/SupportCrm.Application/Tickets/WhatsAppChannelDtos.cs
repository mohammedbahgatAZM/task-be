namespace SupportCrm.Application.Tickets;

public record SendWhatsAppMessageRequest(string Body, string ChangedBy, IReadOnlyList<Guid>? AttachmentIds, bool IsTemplate);
public record RecordWhatsAppStatusRequest(Guid TicketMessageId, string Status, string? Detail); // Status: "Delivered" | "Read" | "Failed"
