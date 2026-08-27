namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;
using SupportCrm.Application.KnowledgeBase;

public class TicketSolutionSuggestionService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    ISolutionSuggestionFeedbackRepository feedbackRepository,
    KbSearchService kbSearchService,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<KbSearchResultDto>> GetSuggestionsAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundForAiException(ticketId.ToString());
        var messages = await messageRepository.GetMessagesAsync(ticketId, ct);

        // Rebuilt fresh from current content on every call — no caching — so results
        // naturally "update as the conversation develops" without any diffing logic.
        var conversationText = string.Join(" ", new[] { ticket.Subject, ticket.Description }
            .Concat(messages.OrderByDescending(m => m.CreatedAtUtc).Take(5).Select(m => m.Body))
            .Where(t => !string.IsNullOrWhiteSpace(t)));

        var response = await kbSearchService.SearchAsync(conversationText, take: 5, ct);
        return response.Results.Where(r => r.ContentType is "Article" or "Guide").ToList();
    }

    public async Task FlagIrrelevantAsync(Guid ticketId, FlagSolutionSuggestionRequest request, CancellationToken ct)
    {
        var feedback = new SolutionSuggestionFeedback(ticketId, request.ContentType, request.ContentId, request.FlaggedByName, timeProvider.GetUtcNow());
        await feedbackRepository.AddAsync(feedback, ct);
        await feedbackRepository.SaveChangesAsync(ct);
    }
}
