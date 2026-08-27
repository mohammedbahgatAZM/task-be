namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken ct);
    Task AddAsync(Team team, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
