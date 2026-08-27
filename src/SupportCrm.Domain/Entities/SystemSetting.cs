namespace SupportCrm.Domain.Entities;

public class SystemSetting
{
    public Guid Id { get; private set; }
    public string Key { get; private set; } = default!;
    public string Value { get; private set; } = default!;
    public string UpdatedBy { get; private set; } = default!;
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private SystemSetting() { }

    public SystemSetting(string key, string value, string updatedBy, DateTimeOffset updatedAtUtc)
    {
        Id = Guid.NewGuid();
        Key = key;
        Value = value;
        UpdatedBy = updatedBy;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetValue(string value, string updatedBy, DateTimeOffset updatedAtUtc)
    {
        Value = value;
        UpdatedBy = updatedBy;
        UpdatedAtUtc = updatedAtUtc;
    }
}
