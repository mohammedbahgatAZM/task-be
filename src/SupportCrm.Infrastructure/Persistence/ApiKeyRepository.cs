namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ApiKeyRepository(SupportCrmDbContext dbContext) : IApiKeyRepository
{
    public Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct) => dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);
    public Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken ct) => dbContext.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == keyHash, ct);
    public async Task<IReadOnlyList<ApiKey>> GetAllAsync(CancellationToken ct) => await dbContext.ApiKeys.ToListAsync(ct);
    public Task AddAsync(ApiKey apiKey, CancellationToken ct) { dbContext.ApiKeys.Add(apiKey); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
