namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Entities;

/// <summary>
/// Suggests a category + priority for a new ticket. No real classifier exists in this
/// codebase — register <see cref="MockAiCategorizationProvider"/> until one does. Its
/// "confidence" is a normalized keyword-overlap score (0-100), not a calibrated probability.
/// </summary>
public interface IAiCategorizationProvider
{
    AiCategorizationResult Categorize(string subject, string? description, IReadOnlyList<TicketCategory> activeCategories);
}
