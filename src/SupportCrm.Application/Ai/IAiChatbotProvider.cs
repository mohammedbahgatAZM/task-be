namespace SupportCrm.Application.Ai;

using SupportCrm.Application.KnowledgeBase;

/// <summary>
/// Answers a chatbot question. No real conversational model exists in this codebase —
/// register <see cref="MockAiChatbotProvider"/> until one does. Grounded entirely in
/// Knowledge Base search results, template-based, not generative.
/// </summary>
public interface IAiChatbotProvider
{
    AiChatbotAnswer Answer(string question, IReadOnlyList<KbSearchResultDto> groundingResults, string language);
}
