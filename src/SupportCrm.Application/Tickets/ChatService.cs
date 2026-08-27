namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ChatService(
    IChatRepository chatRepository,
    IAgentRepository agentRepository,
    TicketIngestionService ingestionService,
    TimeProvider timeProvider)
{
    private const int AverageHandlingSecondsPerQueuedChat = 180; // naive constant, not a statistical estimate

    public async Task<ChatSessionDto> StartAsync(StartChatRequest request, CancellationToken ct)
    {
        var session = new ChatSession(request.CustomerName.Trim(), request.CustomerContactValue?.Trim(), timeProvider.GetUtcNow());
        await chatRepository.AddAsync(session, ct);

        // FIFO-to-any-available-agent — no skill matching (no skills taxonomy exists).
        var agents = await agentRepository.GetAllAsync(ct);
        var availableAgent = ChatAgentAssignment.PickAvailable(agents);
        if (availableAgent is not null)
            session.AssignAgent(availableAgent.Id);

        await chatRepository.SaveChangesAsync(ct);
        return ToDto(session);
    }

    public async Task<ChatQueueStatusDto> GetQueueStatusAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await chatRepository.GetByIdAsync(sessionId, ct) ?? throw new ChatSessionNotFoundException(sessionId);
        var position = session.Status == ChatSessionStatus.Queued
            ? await chatRepository.CountQueuedAheadOfAsync(session.StartedAtUtc, ct) + 1
            : 0;
        return new ChatQueueStatusDto(position, position * AverageHandlingSecondsPerQueuedChat, session.CustomerIsTyping, session.AgentIsTyping);
    }

    public async Task<ChatMessageDto> AddMessageAsync(Guid sessionId, SendChatMessageRequest request, CancellationToken ct)
    {
        _ = await chatRepository.GetByIdAsync(sessionId, ct) ?? throw new ChatSessionNotFoundException(sessionId);
        var message = new ChatMessage(sessionId, request.Body.Trim(), request.IsFromCustomer, timeProvider.GetUtcNow());
        await chatRepository.AddMessageAsync(message, ct);
        await chatRepository.SaveChangesAsync(ct);
        return new ChatMessageDto(message.Id, message.Body, message.IsFromCustomer, message.SentAtUtc);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid sessionId, CancellationToken ct) =>
        (await chatRepository.GetMessagesAsync(sessionId, ct))
            .OrderBy(m => m.SentAtUtc)
            .Select(m => new ChatMessageDto(m.Id, m.Body, m.IsFromCustomer, m.SentAtUtc))
            .ToList();

    public async Task SetTypingAsync(Guid sessionId, SetTypingRequest request, CancellationToken ct)
    {
        var session = await chatRepository.GetByIdAsync(sessionId, ct) ?? throw new ChatSessionNotFoundException(sessionId);
        session.SetTyping(request.IsCustomer, request.IsTyping);
        await chatRepository.SaveChangesAsync(ct);
    }

    public async Task<Guid> CompleteAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await chatRepository.GetByIdAsync(sessionId, ct) ?? throw new ChatSessionNotFoundException(sessionId);
        var messages = await chatRepository.GetMessagesAsync(sessionId, ct);

        // Fold the transcript into the shared ingestion path as one inbound event rather than
        // replaying each ChatMessage individually — IngestInboundMessageAsync only models
        // customer-authored inbound content, which doesn't fit replaying the agent's messages too.
        var orderedMessages = messages.OrderBy(m => m.SentAtUtc).ToList();
        var transcript = ChatTranscriptFormatter.Format(orderedMessages);

        var ticket = await ingestionService.IngestInboundMessageAsync(
            new IngestInboundMessageRequest(TicketChannel.Chat, session.CustomerName, session.CustomerContactValue, "Live chat transcript", transcript), ct);

        var now = timeProvider.GetUtcNow();
        session.Complete(ticket.Id, now);
        await chatRepository.SaveChangesAsync(ct);
        return ticket.Id;
    }

    private static ChatSessionDto ToDto(ChatSession s) => new(s.Id, s.Status, s.Mode, s.AssignedAgentId, s.ResultingTicketId, s.StartedAtUtc);
}
