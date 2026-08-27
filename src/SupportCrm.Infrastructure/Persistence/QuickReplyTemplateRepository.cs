namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class QuickReplyTemplateRepository(SupportCrmDbContext dbContext) : IQuickReplyTemplateRepository
{
    public Task<QuickReplyTemplate?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.QuickReplyTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<QuickReplyTemplate>> GetAllAsync(CancellationToken ct) =>
        await dbContext.QuickReplyTemplates.ToListAsync(ct);

    public Task AddAsync(QuickReplyTemplate template, CancellationToken ct)
    {
        dbContext.QuickReplyTemplates.Add(template);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
