namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SystemSettingRepository(SupportCrmDbContext dbContext) : ISystemSettingRepository
{
    public async Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken ct) => await dbContext.SystemSettings.ToListAsync(ct);
    public Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct) => dbContext.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
    public Task AddAsync(SystemSetting setting, CancellationToken ct) { dbContext.SystemSettings.Add(setting); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
