namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IQuickReplyTemplateRepository
{
    Task<QuickReplyTemplate?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<QuickReplyTemplate>> GetAllAsync(CancellationToken ct);
    Task AddAsync(QuickReplyTemplate template, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
