namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class DepartmentRepository(SupportCrmDbContext dbContext) : IDepartmentRepository
{
    public Task<Department?> GetByIdAsync(Guid id, CancellationToken ct) => dbContext.Departments.FirstOrDefaultAsync(d => d.Id == id, ct);
    public async Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken ct) => await dbContext.Departments.ToListAsync(ct);
    public Task AddAsync(Department department, CancellationToken ct) { dbContext.Departments.Add(department); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
