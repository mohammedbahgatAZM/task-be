namespace SupportCrm.Application.Ai;

using Microsoft.Extensions.Options;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketCategorizationService(
    ITicketRepository ticketRepository,
    ITicketCategoryRepository categoryRepository,
    ITicketCategorizationSuggestionRepository suggestionRepository,
    IAiCategorizationProvider categorizationProvider,
    IOptions<AiFeaturesOptions> options,
    TimeProvider timeProvider)
{
    // Applies directly onto the in-memory `ticket` and returns field-change entries to append —
    // called from TicketService.CreateAsync BEFORE that method's own SaveChangesAsync, so ticket
    // creation and AI categorization commit in one round-trip, not two.
    public async Task<IReadOnlyList<TicketFieldChangeEntry>> CategorizeOnCreateAsync(Ticket ticket, CancellationToken ct)
    {
        var categories = await categoryRepository.GetActiveAsync(ct);
        var result = categorizationProvider.Categorize(ticket.Subject, ticket.Description, categories);
        var now = timeProvider.GetUtcNow();

        await suggestionRepository.AddAsync(new TicketCategorizationSuggestion(ticket.Id, result.CategoryId, result.Priority, result.ConfidencePercentage, now), ct);

        var fieldChanges = new List<TicketFieldChangeEntry>();
        if (result.CategoryId is not null && result.ConfidencePercentage >= options.Value.CategorizationConfidenceThresholdPercentage)
        {
            var oldCategoryId = ticket.CategoryId;
            var oldPriority = ticket.Priority;
            ticket.SetCategory(result.CategoryId);
            ticket.SetPriority(result.Priority);
            fieldChanges.Add(new TicketFieldChangeEntry(ticket.Id, "Category", oldCategoryId?.ToString(), result.CategoryId?.ToString(), "AI", now));
            fieldChanges.Add(new TicketFieldChangeEntry(ticket.Id, "Priority", oldPriority.ToString(), result.Priority.ToString(), "AI", now));
        }

        return fieldChanges;
    }

    public async Task<TicketCategorizationSuggestionDto?> GetSuggestionAsync(Guid ticketId, CancellationToken ct)
    {
        var suggestion = await suggestionRepository.GetByTicketAsync(ticketId, ct);
        if (suggestion is null) return null;
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct);
        var wasApplied = ticket is not null && ticket.CategoryId == suggestion.SuggestedCategoryId && suggestion.SuggestedCategoryId is not null;
        return new TicketCategorizationSuggestionDto(suggestion.TicketId, suggestion.SuggestedCategoryId, suggestion.SuggestedPriority, suggestion.ConfidencePercentage, wasApplied);
    }

    public Task<IReadOnlyList<Guid>> GetPendingManualCategorizationTicketIdsAsync(CancellationToken ct) =>
        suggestionRepository.GetPendingManualCategorizationTicketIdsAsync(ct);

    // Design note for the executor: this loads one ticket per suggestion (N+1) to compare
    // current vs. suggested category. Acceptable at this app's demo scale (a report endpoint,
    // not a hot path); if the suggestion table grows large, replace with a single SQL join in
    // the repository, same idea as GetPendingManualCategorizationTicketIdsAsync above.
    public async Task<IReadOnlyList<CategorizationAccuracyPointDto>> GetAccuracyReportAsync(CancellationToken ct)
    {
        var suggestions = await suggestionRepository.GetAllAsync(ct);
        var points = new List<(DateOnly Day, bool Matched)>();
        foreach (var s in suggestions)
        {
            var ticket = await ticketRepository.GetByIdAsync(s.TicketId, ct);
            if (ticket is null) continue;
            points.Add((DateOnly.FromDateTime(s.CreatedAtUtc.UtcDateTime), ticket.CategoryId == s.SuggestedCategoryId));
        }

        return points
            .GroupBy(p => p.Day)
            .OrderBy(g => g.Key)
            .Select(g => new CategorizationAccuracyPointDto(g.Key, g.Count(), g.Count(p => p.Matched), g.Count() == 0 ? 0 : Math.Round(100.0 * g.Count(p => p.Matched) / g.Count(), 1)))
            .ToList();
    }
}
