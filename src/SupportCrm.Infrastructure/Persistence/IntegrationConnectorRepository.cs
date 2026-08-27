namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class IntegrationConnectorRepository(SupportCrmDbContext dbContext) : IIntegrationConnectorRepository
{
    public Task<IntegrationConnector?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.IntegrationConnectors.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<IntegrationConnector>> GetAllAsync(CancellationToken ct) =>
        await dbContext.IntegrationConnectors.ToListAsync(ct);

    public async Task<IReadOnlyList<IntegrationConnector>> GetEnabledByTypeAsync(IntegrationConnectorType type, CancellationToken ct) =>
        await dbContext.IntegrationConnectors.Where(c => c.IsEnabled && c.Type == type).ToListAsync(ct);

    public Task AddAsync(IntegrationConnector connector, CancellationToken ct) { dbContext.IntegrationConnectors.Add(connector); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
