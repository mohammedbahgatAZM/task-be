namespace SupportCrm.Infrastructure.Persistence;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SolutionSuggestionFeedbackRepository(SupportCrmDbContext dbContext) : ISolutionSuggestionFeedbackRepository
{
    public Task AddAsync(SolutionSuggestionFeedback feedback, CancellationToken ct)
    {
        dbContext.SolutionSuggestionFeedback.Add(feedback);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
