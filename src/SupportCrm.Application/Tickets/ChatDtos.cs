namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record StartChatRequest(string CustomerName, string? CustomerContactValue);
public record ChatSessionDto(Guid Id, ChatSessionStatus Status, ChatSessionMode Mode, Guid? AssignedAgentId, Guid? ResultingTicketId, DateTimeOffset StartedAtUtc);
public record ChatQueueStatusDto(int QueuePosition, int EstimatedWaitSeconds, bool CustomerIsTyping, bool AgentIsTyping);
public record SendChatMessageRequest(string Body, bool IsFromCustomer);
public record ChatMessageDto(Guid Id, string Body, bool IsFromCustomer, DateTimeOffset SentAtUtc);
public record SetTypingRequest(bool IsCustomer, bool IsTyping);

public class ChatSessionNotFoundException(Guid id) : Exception($"Chat session '{id}' was not found.");
