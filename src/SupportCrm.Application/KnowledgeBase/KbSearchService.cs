namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class KbSearchService(
    IFaqRepository faqRepository,
    IArticleRepository articleRepository,
    IGuideRepository guideRepository,
    ISearchLogRepository searchLogRepository,
    TimeProvider timeProvider)
{
    private const int SnippetContextChars = 80;

    public async Task<KbSearchResponseDto> SearchAsync(string query, int take, CancellationToken ct)
    {
        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0)
            return new KbSearchResponseDto(Array.Empty<KbSearchResultDto>(), 0);

        var faqs = (await faqRepository.SearchAsync(normalizedQuery, ct))
            .Select(f => Score("Faq", f.Id, PickField(f.QuestionEn, f.QuestionAr, normalizedQuery), PickField(f.AnswerEn, f.AnswerAr, normalizedQuery), normalizedQuery));
        var articles = (await articleRepository.SearchPublishedAsync(normalizedQuery, ct))
            .Select(a => Score("Article", a.Id, PickField(a.TitleEn, a.TitleAr, normalizedQuery), PickField(a.BodyEn, a.BodyAr, normalizedQuery), normalizedQuery));
        var guides = (await guideRepository.SearchPublishedAsync(normalizedQuery, ct))
            .Select(g => Score("Guide", g.Id, PickField(g.TitleEn, g.TitleAr, normalizedQuery), PickField(g.BodyEn, g.BodyAr, normalizedQuery), normalizedQuery));

        var combined = faqs.Concat(articles).Concat(guides)
            .OrderByDescending(r => r.Score)
            .Take(take)
            .ToList();

        await searchLogRepository.AddAsync(new SearchLog(normalizedQuery, combined.Count, timeProvider.GetUtcNow()), ct);
        await searchLogRepository.SaveChangesAsync(ct);

        return new KbSearchResponseDto(combined, combined.Count);
    }

    // Picks whichever language field actually matched the query (English preferred on a tie),
    // so a caller searching in Arabic gets an Arabic title/snippet back, not a blank English one.
    private static string PickField(string? en, string? ar, string query) =>
        (en is not null && en.Contains(query, StringComparison.OrdinalIgnoreCase)) || ar is null ? en ?? ar ?? "" : ar;

    private static KbSearchResultDto Score(string contentType, Guid id, string title, string body, string query)
    {
        var titleMatch = title.Contains(query, StringComparison.OrdinalIgnoreCase);
        var bodyMatch = body.Contains(query, StringComparison.OrdinalIgnoreCase);
        var score = (titleMatch ? 2.0 : 0.0) + (bodyMatch ? 1.0 : 0.0);
        var snippet = ExtractSnippet(bodyMatch ? body : title, query);
        return new KbSearchResultDto(contentType, id, title, snippet, score);
    }

    internal static string ExtractSnippet(string text, string query)
    {
        var index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return text.Length <= SnippetContextChars * 2 ? text : text[..(SnippetContextChars * 2)];

        var start = Math.Max(0, index - SnippetContextChars);
        var end = Math.Min(text.Length, index + query.Length + SnippetContextChars);
        var match = text.Substring(index, query.Length);
        var before = text[start..index];
        var after = text[(index + query.Length)..end];
        return $"{(start > 0 ? "…" : "")}{before}**{match}**{after}{(end < text.Length ? "…" : "")}";
    }
}
