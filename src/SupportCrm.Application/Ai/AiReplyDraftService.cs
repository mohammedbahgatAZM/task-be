namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;
using SupportCrm.Application.KnowledgeBase;

public class AiReplyDraftService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    KbSearchService kbSearchService,
    IAiReplyDraftProvider draftProvider)
{
    public async Task<AiReplyDraftDto> DraftAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundForAiException(ticketId.ToString());
        var messages = await messageRepository.GetMessagesAsync(ticketId, ct);

        var latestCustomerMessage = messages
            .Where(m => m.AuthorKind == "Customer")
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => m.Body)
            .FirstOrDefault() ?? ticket.Description ?? ticket.Subject;

        var language = AiLanguageDetector.Detect(latestCustomerMessage);
        var grounding = await kbSearchService.SearchAsync(latestCustomerMessage, take: 3, ct);
        var draftText = draftProvider.Draft(latestCustomerMessage, grounding.Results, language);

        return new AiReplyDraftDto(draftText, language);
    }
}
