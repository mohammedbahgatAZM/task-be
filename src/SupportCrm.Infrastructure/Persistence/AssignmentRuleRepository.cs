namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AssignmentRuleRepository(SupportCrmDbContext dbContext) : IAssignmentRuleRepository
{
    public async Task<IReadOnlyList<AssignmentRule>> GetActiveOrderedAsync(CancellationToken ct) =>
        await dbContext.AssignmentRules.Where(r => r.IsActive).OrderBy(r => r.SortOrder).ToListAsync(ct);

    public Task AddAsync(AssignmentRule rule, CancellationToken ct)
    {
        dbContext.AssignmentRules.Add(rule);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
