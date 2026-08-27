namespace SupportCrm.Application.Ai;

using SupportCrm.Application.KnowledgeBase;

public class MockAiChatbotProvider : IAiChatbotProvider
{
    public AiChatbotAnswer Answer(string question, IReadOnlyList<KbSearchResultDto> groundingResults, string language)
    {
        var top = groundingResults.FirstOrDefault();
        if (top is null)
        {
            return new AiChatbotAnswer(
                language == "ar"
                    ? "لم أجد إجابة مناسبة لسؤالك. هل ترغب في التحدث مع أحد الموظفين أو إنشاء تذكرة؟"
                    : "I couldn't find a good answer to that. Would you like to talk to a human agent, or should I create a ticket for you?",
                CanResolve: false);
        }

        return new AiChatbotAnswer(
            language == "ar"
                ? $"وجدت هذا بخصوص \"{top.Title}\": {top.Snippet} هل هذا يجيب على سؤالك؟"
                : $"I found this regarding \"{top.Title}\": {top.Snippet} Does this answer your question?",
            CanResolve: true);
    }
}
