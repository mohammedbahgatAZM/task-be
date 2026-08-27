namespace SupportCrm.Application.CustomerPortal;

using SupportCrm.Domain.Entities;

public record CustomerTicketSummaryDto(Guid Id, string ReferenceNumber, string Subject, TicketStatus Status, TicketPriority Priority, Guid? CategoryId, DateTimeOffset CreatedAtUtc, DateTimeOffset LastUpdatedAtUtc);
public record CustomerTicketListQuery(TicketStatus? Status, Guid? CategoryId, DateTimeOffset? From, DateTimeOffset? To, string? Query);
public record AddPortalReplyRequest(Guid CustomerId, string CustomerName, string Body);

public class TicketOwnershipException(Guid ticketId) : Exception($"Ticket '{ticketId}' does not belong to the specified customer.");

public record ReopenTicketRequest(Guid CustomerId, string CustomerName);

public record LogFaqImpressionRequest(string DraftSessionId);
public record MarkDraftSessionConvertedRequest(string DraftSessionId);
public record FaqDeflectionReportItemDto(Guid FaqId, int TotalImpressions, int LedToTicketCount, double DeflectionRatePercentage);

public record SubmitTicketFeedbackRequest(Guid CustomerId, int Rating, string? Comment);
public record TicketFeedbackDto(Guid TicketId, int Rating, string? Comment, DateTimeOffset SubmittedAtUtc);
