namespace SupportCrm.Application.Integrations;

using SupportCrm.Domain.Entities;

// INT-1 — API keys. The raw key is only ever present in ApiKeyCreatedDto, the one response
// returned at creation time; every other read exposes ApiKeyDto (no secret material).
public record CreateApiKeyRequest(string Name, IReadOnlyList<string> Scopes);
public record ApiKeyCreatedDto(Guid Id, string Name, string RawKey, IReadOnlyList<string> Scopes, DateTimeOffset CreatedAtUtc);
public record ApiKeyDto(Guid Id, string Name, IReadOnlyList<string> Scopes, bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset? LastUsedAtUtc);

// INT-1 — webhooks. Same "secret shown once" shape as API keys.
public record CreateWebhookRequest(string Url, IReadOnlyList<string> EventTypes);
public record WebhookCreatedDto(Guid Id, string Url, string Secret, IReadOnlyList<string> EventTypes, DateTimeOffset CreatedAtUtc);
public record WebhookDto(Guid Id, string Url, IReadOnlyList<string> EventTypes, bool IsActive, DateTimeOffset CreatedAtUtc);
public record WebhookDeliveryDto(Guid Id, Guid WebhookSubscriptionId, string EventType, bool Success, int? StatusCode, string? ErrorMessage, DateTimeOffset AttemptedAtUtc);

// INT-3/INT-4 — the configurable connector framework.
public record CreateConnectorRequest(IntegrationConnectorType Type, string Name, string ConfigJson);
public record UpdateConnectorConfigRequest(string ConfigJson);
public record ConnectorDto(Guid Id, IntegrationConnectorType Type, string Name, string ConfigJson, bool IsEnabled, DateTimeOffset? LastTestedAtUtc, bool? LastTestSucceeded, DateTimeOffset? LastSyncAtUtc);
public record ConnectorTestResultDto(bool Succeeded, string Message);

// INT-2 — ERP bi-directional sync log.
public record ErpSyncLogDto(Guid Id, Guid ConnectorId, Guid CustomerId, ErpSyncStatus Status, string Message, DateTimeOffset OccurredAtUtc);

// INT-2/INT-4 — the external-data panel shown on the ticket/customer profile. Every snippet
// carries its own source label + fetched-at timestamp and success flag, so a failed connector
// shows as "unavailable" for its own card without blocking the others or the ticket itself.
public record ExternalDataFieldDto(string Label, string Value);
public record ExternalDataSnippetDto(string SourceName, bool Success, string? ErrorMessage, DateTimeOffset FetchedAtUtc, IReadOnlyList<ExternalDataFieldDto> Fields);
