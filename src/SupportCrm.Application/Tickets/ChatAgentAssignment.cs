namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

// Shared by ChatService.StartAsync (human-queue entry, Communication Channels CC-3) and
// AiChatbotService.RequestHumanAsync (bot-to-human escalation, AI Features AI-5) — one
// FIFO-to-any-available-agent policy, not two copies of it.
public static class ChatAgentAssignment
{
    public static Agent? PickAvailable(IReadOnlyList<Agent> agents) => agents.FirstOrDefault(a => a.IsAvailable);
}
