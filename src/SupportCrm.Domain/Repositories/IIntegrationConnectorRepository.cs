namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IIntegrationConnectorRepository
{
    Task<IntegrationConnector?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<IntegrationConnector>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<IntegrationConnector>> GetEnabledByTypeAsync(IntegrationConnectorType type, CancellationToken ct);
    Task AddAsync(IntegrationConnector connector, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
