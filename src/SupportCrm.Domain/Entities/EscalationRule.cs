namespace SupportCrm.Domain.Entities;

// Rules are evaluated in SortOrder order by EscalationRuleEngine; the first active rule
// whose CategoryId/Priority conditions match a ticket applies (same "first match wins"
// convention as AssignmentRule). A rule with both conditions null applies to every ticket —
// unlike AssignmentRule, this is intentionally allowed here, since a catch-all baseline
// escalation policy is a common, valid setup.
public class EscalationRule
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public int SortOrder { get; private set; }
    public Guid? CategoryId { get; private set; }
    public TicketPriority? Priority { get; private set; }
    public bool IsActive { get; private set; } = true;

    private EscalationRule() { } // EF Core

    public EscalationRule(string name, int sortOrder, Guid? categoryId, TicketPriority? priority)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Id = Guid.NewGuid();
        Name = name;
        SortOrder = sortOrder;
        CategoryId = categoryId;
        Priority = priority;
    }

    public void Deactivate() => IsActive = false;

    public bool Matches(Guid? ticketCategoryId, TicketPriority ticketPriority) =>
        (CategoryId is null || CategoryId == ticketCategoryId) &&
        (Priority is null || Priority == ticketPriority);
}
