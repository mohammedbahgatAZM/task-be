namespace SupportCrm.Application.Integrations;

using System.Security.Cryptography;
using System.Text;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

// INT-1 — "API access is secured via authentication tokens/API keys scoped by permission."
// Raw keys are never persisted — only their SHA-256 hash, checked by ApiKeyAuthenticationHandler
// on every request to the external-facing api/integrations/v1/* controllers.
public class ApiKeyService(IApiKeyRepository repository, TimeProvider timeProvider)
{
    // The fixed catalog of scopes an API key can be granted — deliberately small and explicit
    // rather than free-text, so a typo'd scope can never silently grant nothing (and look like a
    // bug) or everything (and look like a security hole).
    public static readonly IReadOnlyList<string> KnownScopes =
        ["customers.read", "customers.write", "tickets.read", "tickets.write", "users.read"];

    public async Task<ApiKeyCreatedDto> CreateAsync(CreateApiKeyRequest request, string createdBy, CancellationToken ct)
    {
        var unknown = request.Scopes.Where(s => !KnownScopes.Contains(s)).ToList();
        if (unknown.Count > 0)
            throw new ArgumentException($"Unknown scope(s): {string.Join(", ", unknown)}.", nameof(request));

        var rawKey = $"sk_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}";
        var apiKey = new ApiKey(request.Name, Hash(rawKey), request.Scopes, createdBy, timeProvider.GetUtcNow());
        await repository.AddAsync(apiKey, ct);
        await repository.SaveChangesAsync(ct);
        return new ApiKeyCreatedDto(apiKey.Id, apiKey.Name, rawKey, apiKey.ScopeList, apiKey.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<ApiKeyDto>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task RevokeAsync(Guid id, CancellationToken ct)
    {
        var apiKey = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"API key '{id}' was not found.");
        apiKey.Revoke();
        await repository.SaveChangesAsync(ct);
    }

    // Called once per request by ApiKeyAuthenticationHandler. Returns null for any unrecognized
    // or revoked key — the handler treats null identically whether the key never existed or was
    // since revoked, so revocation can't be probed for via response-shape differences.
    public async Task<ApiKey?> ValidateAsync(string rawKey, CancellationToken ct)
    {
        var apiKey = await repository.GetByKeyHashAsync(Hash(rawKey), ct);
        if (apiKey is null || !apiKey.IsActive) return null;
        apiKey.RecordUsage(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(ct);
        return apiKey;
    }

    private static string Hash(string rawKey) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));

    private static ApiKeyDto ToDto(ApiKey k) => new(k.Id, k.Name, k.ScopeList, k.IsActive, k.CreatedAtUtc, k.LastUsedAtUtc);
}
