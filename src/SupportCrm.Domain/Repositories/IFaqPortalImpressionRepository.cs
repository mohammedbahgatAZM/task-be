namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IFaqPortalImpressionRepository
{
    Task AddAsync(FaqPortalImpression impression, CancellationToken ct);
    Task<IReadOnlyList<FaqPortalImpression>> GetBySessionAsync(string draftSessionId, CancellationToken ct);
    Task<IReadOnlyList<FaqPortalImpression>> GetAllAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
