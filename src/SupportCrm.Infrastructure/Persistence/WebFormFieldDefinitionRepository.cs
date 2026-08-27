namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class WebFormFieldDefinitionRepository(SupportCrmDbContext dbContext) : IWebFormFieldDefinitionRepository
{
    public async Task<IReadOnlyList<WebFormFieldDefinition>> GetByCategoryAsync(Guid categoryId, CancellationToken ct) =>
        await dbContext.WebFormFieldDefinitions.Where(d => d.CategoryId == categoryId).ToListAsync(ct);

    public Task AddAsync(WebFormFieldDefinition definition, CancellationToken ct)
    {
        dbContext.WebFormFieldDefinitions.Add(definition);
        return Task.CompletedTask;
    }

    public Task<WebFormFieldDefinition?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.WebFormFieldDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task DeleteAsync(WebFormFieldDefinition definition, CancellationToken ct)
    {
        dbContext.WebFormFieldDefinitions.Remove(definition);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
