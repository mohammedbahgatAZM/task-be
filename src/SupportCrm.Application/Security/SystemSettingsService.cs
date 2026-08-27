namespace SupportCrm.Application.Security;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SystemSettingsService(ISystemSettingRepository repository, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken ct)
    {
        var stored = (await repository.GetAllAsync(ct)).ToDictionary(s => s.Key, s => s.Value);
        return SystemSettingCatalog.Definitions
            .Select(d => new SystemSettingDto(d.Key, d.DisplayName, d.ValueType, stored.TryGetValue(d.Key, out var v) ? v : d.DefaultValue))
            .ToList();
    }

    // True dry run — never touches the repository. The frontend always calls this before
    // offering "Apply"; Apply itself re-validates anyway, so this is a genuine preview, not the
    // only thing standing between a bad value and the database.
    public Task<ValidationResultDto> ValidateAsync(ValidateSettingsRequest request, CancellationToken ct)
    {
        var errors = new Dictionary<string, string>();
        foreach (var (key, value) in request.Changes)
        {
            var definition = SystemSettingCatalog.Find(key);
            if (definition is null) { errors[key] = "Unknown setting key."; continue; }
            var error = definition.Validate(value);
            if (error is not null) errors[key] = error;
        }
        return Task.FromResult(new ValidationResultDto(errors.Count == 0, errors));
    }

    public async Task<ValidationResultDto> ApplyAsync(ApplySettingsRequest request, string changedBy, CancellationToken ct)
    {
        var validation = await ValidateAsync(new ValidateSettingsRequest(request.Changes), ct);
        if (!validation.IsValid) return validation; // nothing persisted

        var now = timeProvider.GetUtcNow();
        foreach (var (key, value) in request.Changes)
        {
            var existing = await repository.GetByKeyAsync(key, ct);
            if (existing is null) await repository.AddAsync(new SystemSetting(key, value, changedBy, now), ct);
            else existing.SetValue(value, changedBy, now);
        }
        await repository.SaveChangesAsync(ct);
        return validation;
    }
}
