namespace SupportCrm.Domain.Entities;

// INT-3/INT-4 — a configured connection to one external provider/system. ConfigJson holds
// whatever credentials/settings that connector type needs (e.g. {"apiKey":"..."} for a
// provider, {"baseUrl":"..."} for an ERP) — validated shallowly (non-empty) by
// IntegrationConnectorService, never interpreted by the domain layer itself.
public class IntegrationConnector
{
    public Guid Id { get; private set; }
    public IntegrationConnectorType Type { get; private set; }
    public string Name { get; private set; } = default!;
    public string ConfigJson { get; private set; } = "{}";
    public bool IsEnabled { get; private set; } = true;
    public DateTimeOffset? LastTestedAtUtc { get; private set; }
    public bool? LastTestSucceeded { get; private set; }
    public DateTimeOffset? LastSyncAtUtc { get; private set; }

    private IntegrationConnector() { }

    public IntegrationConnector(IntegrationConnectorType type, string name, string configJson)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Connector name is required.", nameof(name));
        Id = Guid.NewGuid();
        Type = type;
        Name = name.Trim();
        ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;
    }

    public void UpdateConfig(string configJson) => ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;
    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;
    public void RecordTestResult(bool succeeded, DateTimeOffset now)
    {
        LastTestedAtUtc = now;
        LastTestSucceeded = succeeded;
    }
    public void RecordSync(DateTimeOffset now) => LastSyncAtUtc = now;
}
