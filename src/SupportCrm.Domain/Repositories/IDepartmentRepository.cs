namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken ct);
    Task AddAsync(Department department, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
