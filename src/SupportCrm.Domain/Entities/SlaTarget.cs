namespace SupportCrm.Domain.Entities;

// Resolution precedence when multiple active targets match one ticket: the most specific
// wins — Tier+Category > Category-only > Priority-only (Priority is always required and
// always matches exactly). See SlaTargetService.ResolveAsync, which orders by Specificity().
public class SlaTarget
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public TicketPriority Priority { get; private set; }
    public Guid? CategoryId { get; private set; }
    public CustomerTier? Tier { get; private set; }
    public int ResponseTargetMinutes { get; private set; }
    public int ResolutionTargetMinutes { get; private set; }
    public bool IsActive { get; private set; } = true;

    private SlaTarget() { } // EF Core

    public SlaTarget(string name, TicketPriority priority, Guid? categoryId, CustomerTier? tier, int responseTargetMinutes, int resolutionTargetMinutes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (responseTargetMinutes <= 0)
            throw new ArgumentException("Response target must be positive.", nameof(responseTargetMinutes));
        if (resolutionTargetMinutes < responseTargetMinutes)
            throw new ArgumentException("Resolution target must be at least the response target.", nameof(resolutionTargetMinutes));

        Id = Guid.NewGuid();
        Name = name;
        Priority = priority;
        CategoryId = categoryId;
        Tier = tier;
        ResponseTargetMinutes = responseTargetMinutes;
        ResolutionTargetMinutes = resolutionTargetMinutes;
    }

    public void Deactivate() => IsActive = false;

    public int Specificity() => (CategoryId is not null ? 1 : 0) + (Tier is not null ? 1 : 0);
}
