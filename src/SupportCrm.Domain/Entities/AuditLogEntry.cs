namespace SupportCrm.Domain.Entities;

// Append-only by construction — no setters beyond the constructor, and no endpoint anywhere
// (including this feature's own) accepts an update or delete for this entity.
public class AuditLogEntry
{
    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public string UserEmail { get; private set; } = default!; // denormalized snapshot — survives the user later being deleted
    public string HttpMethod { get; private set; } = default!;
    public string Path { get; private set; } = default!;
    public string ActionSummary { get; private set; } = default!;
    public string? IpAddress { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }

    private AuditLogEntry() { }

    public AuditLogEntry(Guid? userId, string userEmail, string httpMethod, string path, string actionSummary, string? ipAddress, DateTimeOffset occurredAtUtc)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        UserEmail = string.IsNullOrWhiteSpace(userEmail) ? "anonymous" : userEmail;
        HttpMethod = httpMethod;
        Path = path;
        ActionSummary = actionSummary;
        IpAddress = ipAddress;
        OccurredAtUtc = occurredAtUtc;
    }
}
