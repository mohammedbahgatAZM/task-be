namespace SupportCrm.Application.Integrations;

using SupportCrm.Domain.Entities;

// INT-4 — "external systems such as billing or inventory." A second, independent connector
// implementation proving the framework in IExternalSystemConnector genuinely supports more than
// just ERP — deliberately simpler than MockErpConnector (no sync/conflict logic, read-only data).
public class MockBillingConnector : IExternalSystemConnector
{
    public IntegrationConnectorType Type => IntegrationConnectorType.Billing;

    public Task<ExternalDataSnippetDto> FetchCustomerDataAsync(IntegrationConnector connector, Customer customer, DateTimeOffset now, CancellationToken ct)
    {
        var seed = Math.Abs(customer.Id.GetHashCode());
        var fields = new List<ExternalDataFieldDto>
        {
            new("Plan", seed % 3 == 0 ? "Enterprise" : seed % 3 == 1 ? "Business" : "Standard"),
            new("Outstanding balance", $"${seed % 2000 / 100m:0.00}"),
            new("Payment method on file", seed % 2 == 0 ? "Card on file" : "Invoiced (Net 30)")
        };
        return Task.FromResult(new ExternalDataSnippetDto("Billing", true, null, now, fields));
    }
}
