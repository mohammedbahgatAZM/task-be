namespace SupportCrm.Application.Ai;

using SupportCrm.Application.KnowledgeBase;

/// <summary>
/// Drafts a reply. No real LLM exists in this codebase — register
/// <see cref="MockAiReplyDraftProvider"/> until one does. Template-based, not generative.
/// </summary>
public interface IAiReplyDraftProvider
{
    string Draft(string latestCustomerMessage, IReadOnlyList<KbSearchResultDto> groundingResults, string language);
}
