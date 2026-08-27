namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IAssignmentRuleRepository
{
    Task<IReadOnlyList<AssignmentRule>> GetActiveOrderedAsync(CancellationToken ct);
    Task AddAsync(AssignmentRule rule, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
