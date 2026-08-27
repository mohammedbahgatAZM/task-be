namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken ct);
    Task<IReadOnlyList<ApiKey>> GetAllAsync(CancellationToken ct);
    Task AddAsync(ApiKey apiKey, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
