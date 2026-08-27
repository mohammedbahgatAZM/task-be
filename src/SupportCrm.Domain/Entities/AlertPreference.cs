namespace SupportCrm.Domain.Entities;

public class AlertPreference
{
    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public bool EmailEnabled { get; private set; }
    public bool PushEnabled { get; private set; }
    public int WarningThresholdPercentage { get; private set; } = 80;
    public DigestFrequency DigestFrequency { get; private set; } = DigestFrequency.None;

    private AlertPreference() { } // EF Core

    public AlertPreference(Guid agentId)
    {
        Id = Guid.NewGuid();
        AgentId = agentId;
    }

    public void Update(bool emailEnabled, bool pushEnabled, int warningThresholdPercentage, DigestFrequency digestFrequency)
    {
        if (warningThresholdPercentage is <= 0 or > 100)
            throw new ArgumentException("Warning threshold must be between 1 and 100.", nameof(warningThresholdPercentage));
        EmailEnabled = emailEnabled;
        PushEnabled = pushEnabled;
        WarningThresholdPercentage = warningThresholdPercentage;
        DigestFrequency = digestFrequency;
    }
}
