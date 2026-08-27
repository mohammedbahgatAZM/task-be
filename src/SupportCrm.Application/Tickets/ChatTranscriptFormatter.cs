namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

// Shared by ChatService.CompleteAsync (human chat -> ticket) and AiChatbotService's
// escalate-to-ticket action — one way to fold a chat transcript into a single inbound
// ticket-ingestion event. "Agent" labels any non-customer message, bot replies included —
// from the transcript's point of view, the bot spoke in the support side's voice.
public static class ChatTranscriptFormatter
{
    public static string Format(IReadOnlyList<ChatMessage> orderedMessages) =>
        orderedMessages.Count > 0
            ? string.Join("\n", orderedMessages.Select(m => $"{(m.IsFromCustomer ? "Customer" : "Agent")}: {m.Body}"))
            : "(no messages)";
}
