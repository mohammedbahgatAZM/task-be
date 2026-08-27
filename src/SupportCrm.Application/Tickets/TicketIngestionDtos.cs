namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record IngestInboundMessageRequest(
    TicketChannel Channel,
    string RequesterName,
    string? RequesterContactValue,
    string Subject,
    string Body);

public record SendEmailReplyRequest(string Body, string ChangedBy, IReadOnlyList<Guid>? AttachmentIds);
public record RecordEmailBounceRequest(Guid TicketMessageId, string Reason);
public record TicketAttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string UploadedByName, DateTimeOffset UploadedAtUtc);
public record TicketMessageDeliveryStatusDto(Guid Id, Guid TicketMessageId, string Status, string? Detail, DateTimeOffset OccurredAtUtc);
