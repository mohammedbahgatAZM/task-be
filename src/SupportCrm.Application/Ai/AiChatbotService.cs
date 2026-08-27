namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;
using SupportCrm.Application.KnowledgeBase;

public class AiChatbotService(
    IChatRepository chatRepository,
    IAgentRepository agentRepository,
    TicketIngestionService ingestionService,
    KbSearchService kbSearchService,
    IAiChatbotProvider chatbotProvider,
    TimeProvider timeProvider)
{
    public async Task<ChatSessionDto> StartAsync(StartChatbotRequest request, CancellationToken ct)
    {
        var session = new ChatSession(request.CustomerName.Trim(), request.CustomerContactValue?.Trim(), timeProvider.GetUtcNow(), ChatSessionMode.Bot);
        await chatRepository.AddAsync(session, ct);
        await chatRepository.SaveChangesAsync(ct);
        return ToDto(session);
    }

    public async Task<ChatbotReplyDto> SendMessageAsync(Guid sessionId, SendChatbotMessageRequest request, CancellationToken ct)
    {
        var session = await chatRepository.GetByIdAsync(sessionId, ct) ?? throw new ChatSessionNotFoundException(sessionId);
        if (session.Mode != ChatSessionMode.Bot)
            throw new InvalidOperationException("This session has been escalated to a human agent — send further messages through the chat-sessions endpoint instead.");

        var body = request.Body.Trim();
        var customerMessage = new ChatMessage(sessionId, body, isFromCustomer: true, timeProvider.GetUtcNow());
        await chatRepository.AddMessageAsync(customerMessage, ct);

        var language = AiLanguageDetector.Detect(body);
        var grounding = await kbSearchService.SearchAsync(body, take: 3, ct);
        var answer = chatbotProvider.Answer(body, grounding.Results, language);

        var botMessage = new ChatMessage(sessionId, answer.ResponseText, isFromCustomer: false, timeProvider.GetUtcNow());
        await chatRepository.AddMessageAsync(botMessage, ct);
        await chatRepository.SaveChangesAsync(ct);

        return new ChatbotReplyDto(answer.ResponseText, answer.CanResolve, language);
    }

    public async Task<ChatSessionDto> RequestHumanAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await chatRepository.GetByIdAsync(sessionId, ct) ?? throw new ChatSessionNotFoundException(sessionId);
        var agents = await agentRepository.GetAllAsync(ct);
        var availableAgent = ChatAgentAssignment.PickAvailable(agents);
        session.RequestHuman(availableAgent?.Id);
        await chatRepository.SaveChangesAsync(ct);
        return ToDto(session);
    }

    public async Task<Guid> CreateTicketAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await chatRepository.GetByIdAsync(sessionId, ct) ?? throw new ChatSessionNotFoundException(sessionId);
        var messages = await chatRepository.GetMessagesAsync(sessionId, ct);
        var orderedMessages = messages.OrderBy(m => m.SentAtUtc).ToList();
        var transcript = ChatTranscriptFormatter.Format(orderedMessages);

        var ticket = await ingestionService.IngestInboundMessageAsync(
            new IngestInboundMessageRequest(TicketChannel.Chat, session.CustomerName, session.CustomerContactValue, "AI chatbot conversation", transcript), ct);

        var now = timeProvider.GetUtcNow();
        session.Complete(ticket.Id, now);
        await chatRepository.SaveChangesAsync(ct);
        return ticket.Id;
    }

    private static ChatSessionDto ToDto(ChatSession s) => new(s.Id, s.Status, s.Mode, s.AssignedAgentId, s.ResultingTicketId, s.StartedAtUtc);
}
