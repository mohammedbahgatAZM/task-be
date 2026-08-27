namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ISystemSettingRepository
{
    Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken ct);
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct);
    Task AddAsync(SystemSetting setting, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
