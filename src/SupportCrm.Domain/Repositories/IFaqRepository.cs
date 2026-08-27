namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IFaqRepository
{
    Task<Faq?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Faq>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<Faq>> GetByCategoryAsync(Guid kbCategoryId, CancellationToken ct);
    Task<IReadOnlyList<Faq>> GetMostUnhelpfulAsync(int take, CancellationToken ct);
    Task<IReadOnlyList<Faq>> SearchAsync(string query, CancellationToken ct);
    Task AddAsync(Faq faq, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
