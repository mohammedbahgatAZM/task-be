namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Entities;

public class MockAiCategorizationProvider : IAiCategorizationProvider
{
    private static readonly (string Keyword, TicketPriority Priority)[] PriorityHints =
    {
        ("urgent", TicketPriority.Urgent), ("down", TicketPriority.Urgent), ("asap", TicketPriority.Urgent), ("critical", TicketPriority.Urgent),
        ("error", TicketPriority.High), ("broken", TicketPriority.High), ("not working", TicketPriority.High)
    };

    public AiCategorizationResult Categorize(string subject, string? description, IReadOnlyList<TicketCategory> activeCategories)
    {
        var text = $"{subject} {description}".ToLowerInvariant();

        TicketCategory? best = null;
        var bestScore = 0;
        foreach (var category in activeCategories)
        {
            var categoryWords = category.Name.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var overlap = categoryWords.Count(w => text.Contains(w));
            if (overlap > bestScore)
            {
                bestScore = overlap;
                best = category;
            }
        }

        // Floor of 0 when nothing matched at all; otherwise 40 + 25 per matched word, capped at
        // 95 — a rough, explicitly-not-calibrated stand-in for a real classifier's probability.
        var confidence = best is null ? 0 : Math.Min(95, 40 + bestScore * 25);

        var priority = TicketPriority.Medium;
        foreach (var (keyword, hintedPriority) in PriorityHints)
        {
            if (text.Contains(keyword)) { priority = hintedPriority; break; }
        }

        return new AiCategorizationResult(best?.Id, priority, confidence);
    }
}
