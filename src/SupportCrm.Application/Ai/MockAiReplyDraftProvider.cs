namespace SupportCrm.Application.Ai;

using SupportCrm.Application.KnowledgeBase;

public class MockAiReplyDraftProvider : IAiReplyDraftProvider
{
    public string Draft(string latestCustomerMessage, IReadOnlyList<KbSearchResultDto> groundingResults, string language)
    {
        var top = groundingResults.FirstOrDefault();
        if (top is null)
        {
            return language == "ar"
                ? "شكرًا لتواصلك معنا. نحن ننظر في مشكلتك وسنرد عليك في أقرب وقت ممكن."
                : "Thank you for reaching out. We're looking into your issue and will get back to you shortly.";
        }

        return language == "ar"
            ? $"شكرًا لتواصلك معنا. بناءً على قاعدة المعرفة لدينا، إليك ما وجدناه بخصوص \"{top.Title}\": {top.Snippet} نأمل أن يساعدك هذا؛ أخبرنا إذا كنت بحاجة إلى مزيد من المساعدة."
            : $"Thank you for reaching out. Based on our knowledge base, here's what we found regarding \"{top.Title}\": {top.Snippet} We hope this helps — let us know if you need further assistance.";
    }
}
