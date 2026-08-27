namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ISolutionSuggestionFeedbackRepository
{
    Task AddAsync(SolutionSuggestionFeedback feedback, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
