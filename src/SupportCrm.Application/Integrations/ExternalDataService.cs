namespace SupportCrm.Application.Integrations;

using SupportCrm.Domain.Repositories;

// INT-2/INT-4 — "relevant external data can be displayed within the ticket or customer profile
// via integration," and "integration failures degrade gracefully (ticket still usable) rather
// than blocking agent work." Every enabled data-providing connector is queried independently;
// one throwing never stops the others, and a failure still returns a labeled, timestamped
// snippet rather than silently vanishing from the panel.
public class ExternalDataService(
    ICustomerRepository customerRepository,
    IIntegrationConnectorRepository connectorRepository,
    IEnumerable<IExternalSystemConnector> connectors,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ExternalDataSnippetDto>> GetForCustomerAsync(Guid customerId, CancellationToken ct)
    {
        var customer = await customerRepository.GetByIdAsync(customerId, ct)
            ?? throw new KeyNotFoundException($"Customer '{customerId}' was not found.");
        var now = timeProvider.GetUtcNow();
        var allConnectors = await connectorRepository.GetAllAsync(ct);
        var enabled = allConnectors.Where(c => c.IsEnabled).ToList();

        var results = new List<ExternalDataSnippetDto>();
        foreach (var connector in enabled)
        {
            var handler = connectors.FirstOrDefault(c => c.Type == connector.Type);
            if (handler is null) continue; // connector type has no data-fetching implementation (e.g. Email/Sms/WhatsApp)

            try
            {
                results.Add(await handler.FetchCustomerDataAsync(connector, customer, now, ct));
            }
            catch (Exception ex)
            {
                results.Add(new ExternalDataSnippetDto(connector.Name, false, ex.Message, now, []));
            }
        }
        return results;
    }
}
