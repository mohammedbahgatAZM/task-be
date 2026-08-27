namespace SupportCrm.Application.Integrations;

using SupportCrm.Domain.Entities;

// INT-2/INT-4 — "an agent can view relevant ERP data (e.g. order/invoice status) from within a
// ticket." No real ERP account exists in this codebase (same documented scope decision as the
// Communication Channels mock senders) — order/invoice figures are deterministically derived
// from the customer id, so the same customer always shows the same simulated data.
public class MockErpConnector : IExternalSystemConnector
{
    public IntegrationConnectorType Type => IntegrationConnectorType.Erp;

    public Task<ExternalDataSnippetDto> FetchCustomerDataAsync(IntegrationConnector connector, Customer customer, DateTimeOffset now, CancellationToken ct)
    {
        var seed = Math.Abs(customer.Id.GetHashCode());
        var orderStatuses = new[] { "Processing", "Shipped", "Delivered", "Backordered" };
        var fields = new List<ExternalDataFieldDto>
        {
            new("Order status", orderStatuses[seed % orderStatuses.Length]),
            new("Last order date", DateOnly.FromDateTime(now.UtcDateTime).AddDays(-(seed % 30)).ToString("yyyy-MM-dd")),
            new("Invoice balance", $"${seed % 5000 / 100m:0.00}"),
            new("ERP account name", SimulateRemoteCompanyName(customer, now))
        };
        return Task.FromResult(new ExternalDataSnippetDto("ERP", true, null, now, fields));
    }

    // Shared with ErpSyncService's conflict-detection logic. Deliberately derived from
    // Customer.Name (immutable after creation) rather than Company (the field ERP sync itself
    // writes to) — basing "what the ERP thinks" on the same field the sync applies changes to
    // would make every local Company edit look like a simultaneous remote change too, and
    // every sync would misreport a Conflict instead of a clean one-sided update. The day-of-year
    // term makes the simulated value drift roughly once every 5 days per customer instead of
    // being permanently frozen — enough to exercise the sync path without flip-flopping on
    // every call within the same day.
    public static string SimulateRemoteCompanyName(Customer customer, DateTimeOffset now)
    {
        var baseName = $"{customer.Name} Corp";
        var seed = Math.Abs((customer.Id.GetHashCode() * 397) ^ now.UtcDateTime.DayOfYear);
        return seed % 5 == 0 ? $"{baseName} Holdings" : baseName;
    }
}
