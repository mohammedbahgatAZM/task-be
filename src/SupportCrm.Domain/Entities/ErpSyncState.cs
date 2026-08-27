namespace SupportCrm.Domain.Entities;

// INT-2 — per-customer bookkeeping for the bi-directional sync's conflict check: what the ERP
// side and the CRM side each looked like as of the last successful sync. This prototype
// bi-directionally syncs Customer.Company only (documented scope note) — a real ERP integration
// would extend this to whatever field set that ERP exposes.
public class ErpSyncState
{
    public Guid CustomerId { get; private set; }
    public DateTimeOffset LastSyncedAtUtc { get; private set; }
    public string? LastSyncedRemoteCompany { get; private set; }
    public string? LastSyncedLocalCompany { get; private set; }

    private ErpSyncState() { }

    public ErpSyncState(Guid customerId, string? remoteCompany, string? localCompany, DateTimeOffset now)
    {
        CustomerId = customerId;
        LastSyncedRemoteCompany = remoteCompany;
        LastSyncedLocalCompany = localCompany;
        LastSyncedAtUtc = now;
    }

    public void Update(string? remoteCompany, string? localCompany, DateTimeOffset now)
    {
        LastSyncedRemoteCompany = remoteCompany;
        LastSyncedLocalCompany = localCompany;
        LastSyncedAtUtc = now;
    }
}
