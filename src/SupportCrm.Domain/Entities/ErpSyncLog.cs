namespace SupportCrm.Domain.Entities;

// INT-2 — one row per sync attempt for one customer, so "failed sync attempts are logged" and
// "sync conflicts are flagged rather than silently overwritten" are both auditable, not just
// momentary in-memory outcomes.
public class ErpSyncLog
{
    public Guid Id { get; private set; }
    public Guid ConnectorId { get; private set; }
    public Guid CustomerId { get; private set; }
    public ErpSyncStatus Status { get; private set; }
    public string Message { get; private set; } = default!;
    public DateTimeOffset OccurredAtUtc { get; private set; }

    private ErpSyncLog() { }

    public ErpSyncLog(Guid connectorId, Guid customerId, ErpSyncStatus status, string message, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        ConnectorId = connectorId;
        CustomerId = customerId;
        Status = status;
        Message = message;
        OccurredAtUtc = now;
    }
}
