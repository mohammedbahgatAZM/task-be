namespace SupportCrm.Application.Integrations;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;

// INT-2 — "customer records can be synced bi-directionally between the CRM and ERP on a defined
// schedule or trigger," with conflicts flagged rather than silently overwritten, and failed
// attempts logged and alerted. This prototype bi-directionally syncs Customer.Company only
// (documented scope note) against MockErpConnector's simulated remote value — there is no real
// ERP to sync against, same decision as every other mock provider in this codebase.
public class ErpSyncService(
    ICustomerRepository customerRepository,
    IIntegrationConnectorRepository connectorRepository,
    IErpSyncRepository syncRepository,
    IAgentRepository agentRepository,
    AgentNotificationService notificationService,
    TimeProvider timeProvider)
{
    // Runs for every customer, once per enabled Erp connector — called on SlaEscalationHostedService's
    // sibling timer (the "defined schedule") and by the manual POST endpoint (the "trigger").
    public async Task SyncAllAsync(CancellationToken ct)
    {
        var connectors = await connectorRepository.GetEnabledByTypeAsync(IntegrationConnectorType.Erp, ct);
        if (connectors.Count == 0) return;

        var customers = await customerRepository.GetAllAsync(ct);
        foreach (var connector in connectors)
        {
            foreach (var customer in customers)
                await SyncCustomerAsync(connector, customer, ct);
            connector.RecordSync(timeProvider.GetUtcNow());
            await connectorRepository.SaveChangesAsync(ct);
        }
    }

    public async Task<ErpSyncLogDto> SyncCustomerAsync(IntegrationConnector connector, Customer customer, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        try
        {
            var remoteCompany = MockErpConnector.SimulateRemoteCompanyName(customer, now);
            var localCompany = customer.Company;
            var state = await syncRepository.GetStateAsync(customer.Id, ct);

            if (state is null)
            {
                // First sync for this customer — nothing to compare against yet, so this
                // establishes the baseline rather than risking a false-positive conflict.
                await syncRepository.UpsertStateAsync(new ErpSyncState(customer.Id, remoteCompany, localCompany, now), ct);
                return await LogAsync(connector.Id, customer.Id, ErpSyncStatus.Synced, "Initial sync baseline established.", now, ct);
            }

            var remoteChanged = remoteCompany != state.LastSyncedRemoteCompany;
            var localChanged = localCompany != state.LastSyncedLocalCompany;

            if (remoteChanged && localChanged)
            {
                // Neither side is overwritten — the conflict is logged and an admin is alerted
                // to resolve it manually. State is left untouched so the same conflict doesn't
                // silently resolve itself as "no change" on the next tick.
                var message = $"Both sides changed since the last sync (CRM: '{state.LastSyncedLocalCompany}' -> '{localCompany}', ERP: '{state.LastSyncedRemoteCompany}' -> '{remoteCompany}'). Not applied.";
                var log = await LogAsync(connector.Id, customer.Id, ErpSyncStatus.Conflict, message, now, ct);
                await AlertSupervisorsAsync(customer, message, ct);
                return log;
            }

            if (remoteChanged)
            {
                customer.SetCompany(remoteCompany);
                await customerRepository.SaveChangesAsync(ct);
                // Mutates the already-tracked state instance rather than constructing a new one
                // with the same key — GetStateAsync already attached it to this DbContext, and
                // EF Core cannot track a second detached instance sharing that primary key.
                state.Update(remoteCompany, remoteCompany, now);
                await syncRepository.SaveChangesAsync(ct);
                return await LogAsync(connector.Id, customer.Id, ErpSyncStatus.Synced, $"Applied ERP-side change: company is now '{remoteCompany}'.", now, ct);
            }

            // Covers both "local changed, remote didn't" (the CRM edit is authoritative and
            // simply becomes the new accepted baseline) and "nothing changed."
            state.Update(remoteCompany, localCompany, now);
            await syncRepository.SaveChangesAsync(ct);
            return await LogAsync(connector.Id, customer.Id, ErpSyncStatus.Synced, localChanged ? "CRM-side change accepted as the new baseline." : "No changes detected.", now, ct);
        }
        catch (Exception ex)
        {
            var log = await LogAsync(connector.Id, customer.Id, ErpSyncStatus.Failed, ex.Message, now, ct);
            await AlertSupervisorsAsync(customer, $"ERP sync failed: {ex.Message}", ct);
            return log;
        }
    }

    public async Task<IReadOnlyList<ErpSyncLogDto>> GetLogsAsync(Guid? customerId, CancellationToken ct) =>
        (await syncRepository.GetLogsAsync(customerId, ct))
            .Select(l => new ErpSyncLogDto(l.Id, l.ConnectorId, l.CustomerId, l.Status, l.Message, l.OccurredAtUtc))
            .ToList();

    private async Task<ErpSyncLogDto> LogAsync(Guid connectorId, Guid customerId, ErpSyncStatus status, string message, DateTimeOffset now, CancellationToken ct)
    {
        var entry = new ErpSyncLog(connectorId, customerId, status, message, now);
        await syncRepository.AddLogAsync(entry, ct);
        await syncRepository.SaveChangesAsync(ct);
        return new ErpSyncLogDto(entry.Id, entry.ConnectorId, entry.CustomerId, entry.Status, entry.Message, entry.OccurredAtUtc);
    }

    private async Task AlertSupervisorsAsync(Customer customer, string message, CancellationToken ct)
    {
        var supervisors = (await agentRepository.GetAllAsync(ct)).Where(a => a.IsSupervisor);
        foreach (var supervisor in supervisors)
            await notificationService.NotifyAsync(supervisor.Id, "ErpSyncIssue", $"{customer.Name}: {message}", null, ct);
    }
}
