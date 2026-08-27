namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class BranchRepository(SupportCrmDbContext dbContext) : IBranchRepository
{
    public Task<Branch?> GetByIdAsync(Guid id, CancellationToken ct) => dbContext.Branches.FirstOrDefaultAsync(b => b.Id == id, ct);
    public async Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken ct) => await dbContext.Branches.ToListAsync(ct);
    public Task AddAsync(Branch branch, CancellationToken ct) { dbContext.Branches.Add(branch); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
