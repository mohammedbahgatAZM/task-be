namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IWebFormFieldDefinitionRepository
{
    Task<IReadOnlyList<WebFormFieldDefinition>> GetByCategoryAsync(Guid categoryId, CancellationToken ct);
    Task AddAsync(WebFormFieldDefinition definition, CancellationToken ct);
    Task<WebFormFieldDefinition?> GetByIdAsync(Guid id, CancellationToken ct);
    Task DeleteAsync(WebFormFieldDefinition definition, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
