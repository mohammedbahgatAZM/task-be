namespace SupportCrm.Application.Integrations;

using System.Text.Json;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;

// INT-3/INT-4 — admin CRUD for the connector framework, plus the "connection test" INT-3 asks
// for. No real provider account exists anywhere in this codebase (established Communication
// Channels scope decision), so the test is a mock shape-validation of ConfigJson, not a live
// call to any provider — documented explicitly, not implied to be more than it is.
public class IntegrationConnectorService(
    IIntegrationConnectorRepository repository,
    IAgentRepository agentRepository,
    AgentNotificationService notificationService,
    TimeProvider timeProvider)
{
    public async Task<ConnectorDto> CreateAsync(CreateConnectorRequest request, CancellationToken ct)
    {
        var connector = new IntegrationConnector(request.Type, request.Name, request.ConfigJson);
        await repository.AddAsync(connector, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(connector);
    }

    public async Task<IReadOnlyList<ConnectorDto>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task<ConnectorDto> UpdateConfigAsync(Guid id, UpdateConnectorConfigRequest request, CancellationToken ct)
    {
        var connector = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Connector '{id}' was not found.");
        connector.UpdateConfig(request.ConfigJson);
        await repository.SaveChangesAsync(ct);
        return ToDto(connector);
    }

    public async Task SetEnabledAsync(Guid id, bool enabled, CancellationToken ct)
    {
        var connector = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Connector '{id}' was not found.");
        if (enabled) connector.Enable(); else connector.Disable();
        await repository.SaveChangesAsync(ct);
    }

    // Mock validation: succeeds when ConfigJson parses as a non-empty JSON object — standing in
    // for "credentials accepted by the provider," which this codebase has no real provider to
    // check against. A malformed/empty config fails the test and alerts supervisors, exactly as
    // "provider outages or authentication failures trigger an alert to the admin" (INT-3) asks.
    public async Task<ConnectorTestResultDto> TestConnectionAsync(Guid id, CancellationToken ct)
    {
        var connector = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Connector '{id}' was not found.");
        var now = timeProvider.GetUtcNow();

        bool succeeded;
        string message;
        try
        {
            using var doc = JsonDocument.Parse(connector.ConfigJson);
            succeeded = doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.EnumerateObject().Any();
            message = succeeded
                ? "Connection test succeeded (mock validation: configuration present)."
                : "Configuration is empty — add credentials/settings before testing.";
        }
        catch (JsonException)
        {
            succeeded = false;
            message = "Configuration is not valid JSON.";
        }

        connector.RecordTestResult(succeeded, now);
        await repository.SaveChangesAsync(ct);

        if (!succeeded)
        {
            var supervisors = (await agentRepository.GetAllAsync(ct)).Where(a => a.IsSupervisor);
            foreach (var supervisor in supervisors)
                await notificationService.NotifyAsync(supervisor.Id, "IntegrationConnectionFailed",
                    $"Connection test failed for '{connector.Name}' ({connector.Type}): {message}", null, ct);
        }

        return new ConnectorTestResultDto(succeeded, message);
    }

    private static ConnectorDto ToDto(IntegrationConnector c) =>
        new(c.Id, c.Type, c.Name, c.ConfigJson, c.IsEnabled, c.LastTestedAtUtc, c.LastTestSucceeded, c.LastSyncAtUtc);
}
