namespace SupportCrm.Domain.Entities;

// INT-1 — external systems authenticate with this instead of a JWT user session. The raw key
// is shown to the admin exactly once at creation time; only its SHA-256 hash is persisted, the
// same "never store the secret itself" discipline as User.PasswordHash.
public class ApiKey
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string KeyHash { get; private set; } = default!;
    // Comma-separated scope names, e.g. "customers.read,tickets.read,tickets.write". Checked by
    // ApiKeyAuthenticationHandler against per-endpoint authorization policies at request time.
    public string Scopes { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public string CreatedBy { get; private set; } = default!;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    private ApiKey() { }

    public ApiKey(string name, string keyHash, IReadOnlyList<string> scopes, string createdBy, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("API key name is required.", nameof(name));
        if (scopes.Count == 0)
            throw new ArgumentException("At least one scope is required.", nameof(scopes));
        Id = Guid.NewGuid();
        Name = name.Trim();
        KeyHash = keyHash;
        Scopes = string.Join(',', scopes);
        CreatedBy = createdBy;
        CreatedAtUtc = now;
    }

    public IReadOnlyList<string> ScopeList => Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void Revoke() => IsActive = false;
    public void RecordUsage(DateTimeOffset now) => LastUsedAtUtc = now;
}
