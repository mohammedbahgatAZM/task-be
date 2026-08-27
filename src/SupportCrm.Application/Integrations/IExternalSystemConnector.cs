namespace SupportCrm.Application.Integrations;

using SupportCrm.Domain.Entities;

// INT-4 — "new integrations can be added through a configurable connector framework where
// possible." One implementation per data-providing IntegrationConnectorType (Erp, Billing,
// Inventory today); ExternalDataService fans out to every enabled connector of these types and
// isolates failures per-connector so one broken integration never blocks the others.
public interface IExternalSystemConnector
{
    IntegrationConnectorType Type { get; }
    Task<ExternalDataSnippetDto> FetchCustomerDataAsync(IntegrationConnector connector, Customer customer, DateTimeOffset now, CancellationToken ct);
}
