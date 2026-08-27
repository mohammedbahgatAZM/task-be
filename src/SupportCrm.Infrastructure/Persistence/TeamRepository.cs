namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TeamRepository(SupportCrmDbContext dbContext) : ITeamRepository
{
    public Task<Team?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.Teams.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken ct) =>
        await dbContext.Teams.ToListAsync(ct);

    public Task AddAsync(Team team, CancellationToken ct)
    {
        dbContext.Teams.Add(team);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
