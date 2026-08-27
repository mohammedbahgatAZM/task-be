namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Entities;

// AI-1 — ticket summaries
public record TicketAiSummaryDto(Guid TicketId, string SummaryText, int SourceMessageCount, DateTimeOffset GeneratedAtUtc, bool IsAiGenerated = true);

public class TicketNotFoundForAiException(string id) : Exception($"Ticket '{id}' was not found.");

// AI-2 — suggested replies
public record AiReplyDraftDto(string DraftText, string DetectedLanguage);

// AI-3 — automatic categorization
public record AiCategorizationResult(Guid? CategoryId, TicketPriority Priority, int ConfidencePercentage);
public record TicketCategorizationSuggestionDto(Guid TicketId, Guid? SuggestedCategoryId, TicketPriority SuggestedPriority, int ConfidencePercentage, bool WasApplied);
public record CategorizationAccuracyPointDto(DateOnly Day, int TotalSuggestions, int MatchingCount, double AccuracyPercentage);

// AI-4 — suggested solutions
public record FlagSolutionSuggestionRequest(string ContentType, Guid ContentId, string FlaggedByName);

// AI-5 — chatbot
public record StartChatbotRequest(string CustomerName, string? CustomerContactValue);
public record SendChatbotMessageRequest(string Body);
public record ChatbotReplyDto(string ResponseText, bool CanResolve, string DetectedLanguage);
public record AiChatbotAnswer(string ResponseText, bool CanResolve);
