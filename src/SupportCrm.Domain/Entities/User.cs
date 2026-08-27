namespace SupportCrm.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public bool MfaEnabled { get; private set; }
    public string? MfaSecret { get; private set; }
    public DateTimeOffset PasswordChangedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTimeOffset? LockedUntilUtc { get; private set; }

    private User() { } // EF Core

    public User(string email, string passwordHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));

        Id = Guid.NewGuid();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        CreatedAtUtc = now;
        PasswordChangedAtUtc = now;
    }

    public void SetPassword(string passwordHash, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        PasswordChangedAtUtc = now;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    // Two-step MFA setup: BeginMfaSetup generates+stores a secret WITHOUT enforcing it yet;
    // ConfirmMfaSetup only flips MfaEnabled once the caller has proven they can produce a valid
    // code — avoids ever locking an account out behind a secret nobody confirmed they can use.
    public void BeginMfaSetup(string secret) => MfaSecret = secret;
    public void ConfirmMfaSetup() => MfaEnabled = true;
    public void DisableMfa() { MfaEnabled = false; MfaSecret = null; }

    public bool IsLockedOut(DateTimeOffset now) => LockedUntilUtc is DateTimeOffset until && until > now;

    public void RegisterFailedLogin(DateTimeOffset now, int maxAttempts, int lockoutMinutes)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxAttempts)
            LockedUntilUtc = now.AddMinutes(lockoutMinutes);
    }

    public void RegisterSuccessfulLogin()
    {
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;
    }
}
