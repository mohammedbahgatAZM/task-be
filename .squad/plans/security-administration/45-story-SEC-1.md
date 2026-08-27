# Story 45 — Users and roles (Story: SEC-1)

---

## Prerequisites

None — first story in this feature, and this codebase's first real authentication system.

---

## Story Goal

1. `User`/`Role` entities, real password hashing, real JWT login (with MFA and password-expiry paths), real RFC 6238 TOTP MFA.
2. Admin user CRUD (create/deactivate/activate/delete) with role assignment.
3. Deactivation takes effect on the user's very next request, not just their next login.
4. A seeded default Admin account so the system is bootstrappable.

---

## Context — Read These Files First

1. `src/SupportCrm.Api/SupportCrm.Api.csproj`, `src/SupportCrm.Infrastructure/SupportCrm.Infrastructure.csproj` — `Microsoft.AspNetCore.Authentication.JwtBearer` and `Microsoft.AspNetCore.Identity.EntityFrameworkCore` are already referenced, unused. No new packages needed.
2. `src/SupportCrm.Api/Program.cs` (all of it) — every line this story's auth-middleware wiring touches.
3. `src/SupportCrm.Application/CustomerPortal/CustomerPortalOptions.cs` — the `IOptions<T>` shape `SecurityOptions`/`JwtOptions` follow.

---

## Backend Tasks

### 1 — Domain

**Create files under `src/SupportCrm.Domain/Entities/`:**

`User.cs`:
```csharp
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
```

`Role.cs`:
```csharp
namespace SupportCrm.Domain.Entities;

public class Role
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public bool IsSystemDefined { get; private set; }

    private Role() { }

    public Role(string name, bool isSystemDefined)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Role name is required.", nameof(name));
        Id = Guid.NewGuid();
        Name = name.Trim();
        IsSystemDefined = isSystemDefined;
    }
}
```

`UserRole.cs`:
```csharp
namespace SupportCrm.Domain.Entities;

public class UserRole
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }

    private UserRole() { }

    public UserRole(Guid userId, Guid roleId)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        RoleId = roleId;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IUserRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    Task DeleteAsync(User user, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetRoleIdsForUserAsync(Guid userId, CancellationToken ct);
    Task SetUserRolesAsync(Guid userId, IReadOnlyList<Guid> roleIds, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IRoleRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<Role>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    Task AddAsync(Role role, CancellationToken ct);
    Task DeleteAsync(Role role, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: options, hashing, TOTP, JWT, `AuthService`, `UserManagementService`

**Create file: `src/SupportCrm.Application/Security/SecurityOptions.cs`**

```csharp
namespace SupportCrm.Application.Security;

public class SecurityOptions
{
    public const string SectionName = "Security";
    public int PasswordMinLength { get; set; } = 10;
    public bool PasswordRequireUppercase { get; set; } = true;
    public bool PasswordRequireLowercase { get; set; } = true;
    public bool PasswordRequireDigit { get; set; } = true;
    public bool PasswordRequireSpecialChar { get; set; } = true;
    public int PasswordMaxAgeDays { get; set; } = 90;
    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}
```

**Create file: `src/SupportCrm.Application/Security/JwtOptions.cs`**

```csharp
namespace SupportCrm.Application.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string SigningKey { get; set; } = default!;
    public string Issuer { get; set; } = "SupportCrm";
    public string Audience { get; set; } = "SupportCrm";
    public int AccessTokenExpiryMinutes { get; set; } = 30;
    public int MfaChallengeExpiryMinutes { get; set; } = 5;
}
```

**Create file: `src/SupportCrm.Application/Security/SecurityDtos.cs`** (this story's subset — Stories 46–48 append their own):

```csharp
namespace SupportCrm.Application.Security;

public record CreateUserRequest(string Email, string InitialPassword, IReadOnlyList<Guid> RoleIds);
public record UserDto(Guid Id, string Email, bool IsActive, bool MfaEnabled, DateTimeOffset PasswordChangedAtUtc, DateTimeOffset CreatedAtUtc, IReadOnlyList<string> RoleNames);
public record SetUserRolesRequest(IReadOnlyList<Guid> RoleIds);

public record LoginRequest(string Email, string Password);
public enum LoginOutcome { Success, RequiresMfa, InvalidCredentials, AccountLocked, AccountDeactivated, PasswordExpired }
public record LoginResultDto(LoginOutcome Outcome, string? AccessToken, DateTimeOffset? ExpiresAtUtc, string? MfaChallengeToken);
public record CompleteMfaLoginRequest(string MfaChallengeToken, string Code);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record MfaSetupDto(string Secret, string ProvisioningUri);
public record ConfirmMfaRequest(string Code);
public record CurrentUserDto(Guid UserId, string Email, IReadOnlyList<string> RoleNames, IReadOnlyList<string> Permissions);

public class SecurityValidationException(IReadOnlyList<string> errors) : Exception(string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
public class UserNotFoundException(Guid id) : Exception($"User '{id}' was not found.");
public class DuplicateEmailException(string email) : Exception($"A user with email '{email}' already exists.");
```

**Create file: `src/SupportCrm.Application/Security/PasswordPolicyValidator.cs`**

```csharp
namespace SupportCrm.Application.Security;

using Microsoft.Extensions.Options;

public class PasswordPolicyValidator(IOptions<SecurityOptions> options)
{
    public IReadOnlyList<string> Validate(string password)
    {
        var o = options.Value;
        var errors = new List<string>();
        if (string.IsNullOrEmpty(password) || password.Length < o.PasswordMinLength)
            errors.Add($"Password must be at least {o.PasswordMinLength} characters.");
        if (o.PasswordRequireUppercase && !password.Any(char.IsUpper))
            errors.Add("Password must contain an uppercase letter.");
        if (o.PasswordRequireLowercase && !password.Any(char.IsLower))
            errors.Add("Password must contain a lowercase letter.");
        if (o.PasswordRequireDigit && !password.Any(char.IsDigit))
            errors.Add("Password must contain a digit.");
        if (o.PasswordRequireSpecialChar && password.All(char.IsLetterOrDigit))
            errors.Add("Password must contain a special character.");
        return errors;
    }
}
```

**Create file: `src/SupportCrm.Application/Security/PasswordHashingService.cs`**

```csharp
namespace SupportCrm.Application.Security;

using Microsoft.AspNetCore.Identity;
using SupportCrm.Domain.Entities;

// Thin wrapper around ASP.NET Core Identity's standalone PBKDF2 hasher — the package is already
// referenced (SupportCrm.Infrastructure.csproj) for a full Identity/EF Identity-store setup this
// codebase deliberately doesn't use; only the hasher class itself is reused.
public class PasswordHashingService
{
    private readonly PasswordHasher<User> hasher = new();

    public string Hash(User user, string password) => hasher.HashPassword(user, password);

    public bool Verify(User user, string password) =>
        hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
```

**Create file: `src/SupportCrm.Application/Security/TotpService.cs`**

```csharp
namespace SupportCrm.Application.Security;

using System.Security.Cryptography;
using System.Text;

// RFC 6238 TOTP — hand-rolled using HMACSHA1 (built into .NET). Genuinely functional with any
// standard authenticator app (Google Authenticator, Authy, ...); no new NuGet dependency.
public class TotpService
{
    private const int StepSeconds = 30;
    private const int Digits = 6;
    private const int ToleranceSteps = 1; // accept the previous/next 30s window too, for clock drift

    public string GenerateSecret() => Base32Encode(RandomNumberGenerator.GetBytes(20)); // 160-bit

    public string GetProvisioningUri(string email, string secret, string issuer = "SupportCrm") =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits={Digits}&period={StepSeconds}";

    public bool ValidateCode(string secret, string code, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != Digits) return false;
        var key = Base32Decode(secret);
        var currentStep = now.ToUnixTimeSeconds() / StepSeconds;
        for (var offset = -ToleranceSteps; offset <= ToleranceSteps; offset++)
            if (ComputeCode(key, currentStep + offset) == code) return true;
        return false;
    }

    private static string ComputeCode(byte[] key, long step)
    {
        var stepBytes = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(stepBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(stepBytes);
        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24) | ((hash[offset + 1] & 0xFF) << 16) | ((hash[offset + 2] & 0xFF) << 8) | (hash[offset + 3] & 0xFF);
        return (binaryCode % (int)Math.Pow(10, Digits)).ToString(new string('0', Digits));
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new StringBuilder();
        int bits = 0, value = 0;
        foreach (var b in data)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(alphabet[(value >> (bits - 5)) & 0x1F]);
                bits -= 5;
            }
        }
        if (bits > 0) sb.Append(alphabet[(value << (5 - bits)) & 0x1F]);
        return sb.ToString();
    }

    private static byte[] Base32Decode(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        int bits = 0, value = 0;
        var output = new List<byte>();
        foreach (var c in base32.ToUpperInvariant())
        {
            var index = alphabet.IndexOf(c);
            if (index < 0) continue;
            value = (value << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return output.ToArray();
    }
}
```

**Create file: `src/SupportCrm.Application/Security/JwtTokenService.cs`**

```csharp
namespace SupportCrm.Application.Security;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SupportCrm.Domain.Entities;

public class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    public const string RoleIdClaimType = "role_id";
    private const string PurposeClaimType = "purpose";
    private const string AccessPurpose = "access";
    private const string MfaChallengePurpose = "mfa_challenge";

    public (string Token, DateTimeOffset ExpiresAtUtc) IssueAccessToken(User user, IReadOnlyList<Role> roles)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(options.Value.AccessTokenExpiryMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(PurposeClaimType, AccessPurpose)
        };
        claims.AddRange(roles.Select(r => new Claim(RoleIdClaimType, r.Id.ToString())));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r.Name)));
        return (WriteToken(claims, now, expiresAt), expiresAt);
    }

    // Deliberately NOT a valid access token — the "purpose" claim keeps a challenge token and a
    // real access token from being interchangeable even though both are signed with the same key.
    public (string Token, DateTimeOffset ExpiresAtUtc) IssueMfaChallengeToken(Guid userId)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(options.Value.MfaChallengeExpiryMinutes);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, userId.ToString()), new(PurposeClaimType, MfaChallengePurpose) };
        return (WriteToken(claims, now, expiresAt), expiresAt);
    }

    public Guid? ValidateMfaChallengeToken(string token)
    {
        var principal = ValidateAndGetPrincipal(token);
        if (principal is null || principal.FindFirst(PurposeClaimType)?.Value != MfaChallengePurpose) return null;
        return Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId) ? userId : null;
    }

    private string WriteToken(List<Claim> claims, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(options.Value.Issuer, options.Value.Audience, claims, now.UtcDateTime, expiresAt.UtcDateTime, credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal? ValidateAndGetPrincipal(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.SigningKey));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = options.Value.Issuer,
            ValidateAudience = true, ValidAudience = options.Value.Audience,
            ValidateIssuerSigningKey = true, IssuerSigningKey = key,
            ValidateLifetime = true
        };
        try { return new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _); }
        catch { return null; }
    }
}
```

**Create file: `src/SupportCrm.Application/Security/AuthService.cs`**

```csharp
namespace SupportCrm.Application.Security;

using Microsoft.Extensions.Options;
using SupportCrm.Domain.Repositories;

public class AuthService(
    IUserRepository userRepository, IRoleRepository roleRepository,
    PasswordHashingService passwordHasher, PasswordPolicyValidator passwordPolicyValidator,
    TotpService totpService, JwtTokenService tokenService,
    IOptions<SecurityOptions> securityOptions, TimeProvider timeProvider)
{
    public async Task<LoginResultDto> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        var now = timeProvider.GetUtcNow();
        if (user is null || !passwordHasher.Verify(user, request.Password))
        {
            if (user is not null)
            {
                user.RegisterFailedLogin(now, securityOptions.Value.MaxFailedLoginAttempts, securityOptions.Value.LockoutMinutes);
                await userRepository.SaveChangesAsync(ct);
            }
            return new LoginResultDto(LoginOutcome.InvalidCredentials, null, null, null);
        }

        if (!user.IsActive) return new LoginResultDto(LoginOutcome.AccountDeactivated, null, null, null);
        if (user.IsLockedOut(now)) return new LoginResultDto(LoginOutcome.AccountLocked, null, null, null);

        var maxAgeDays = securityOptions.Value.PasswordMaxAgeDays;
        if (maxAgeDays > 0 && now > user.PasswordChangedAtUtc.AddDays(maxAgeDays))
            return new LoginResultDto(LoginOutcome.PasswordExpired, null, null, null);

        if (user.MfaEnabled)
        {
            var (challengeToken, _) = tokenService.IssueMfaChallengeToken(user.Id);
            return new LoginResultDto(LoginOutcome.RequiresMfa, null, null, challengeToken);
        }

        return await IssueSuccessAsync(user, ct);
    }

    public async Task<LoginResultDto> CompleteMfaLoginAsync(CompleteMfaLoginRequest request, CancellationToken ct)
    {
        var userId = tokenService.ValidateMfaChallengeToken(request.MfaChallengeToken);
        if (userId is null) return new LoginResultDto(LoginOutcome.InvalidCredentials, null, null, null);

        var user = await userRepository.GetByIdAsync(userId.Value, ct);
        if (user is null || !user.IsActive || user.MfaSecret is null || !totpService.ValidateCode(user.MfaSecret, request.Code, timeProvider.GetUtcNow()))
            return new LoginResultDto(LoginOutcome.InvalidCredentials, null, null, null);

        return await IssueSuccessAsync(user, ct);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        if (!passwordHasher.Verify(user, request.CurrentPassword))
            throw new SecurityValidationException(new[] { "Current password is incorrect." });

        var errors = passwordPolicyValidator.Validate(request.NewPassword);
        if (errors.Count > 0) throw new SecurityValidationException(errors);

        user.SetPassword(passwordHasher.Hash(user, request.NewPassword), timeProvider.GetUtcNow());
        await userRepository.SaveChangesAsync(ct);
    }

    public async Task<MfaSetupDto> BeginMfaSetupAsync(Guid userId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        var secret = totpService.GenerateSecret();
        user.BeginMfaSetup(secret);
        await userRepository.SaveChangesAsync(ct);
        return new MfaSetupDto(secret, totpService.GetProvisioningUri(user.Email, secret));
    }

    public async Task ConfirmMfaSetupAsync(Guid userId, ConfirmMfaRequest request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        if (user.MfaSecret is null || !totpService.ValidateCode(user.MfaSecret, request.Code, timeProvider.GetUtcNow()))
            throw new SecurityValidationException(new[] { "Invalid verification code." });
        user.ConfirmMfaSetup();
        await userRepository.SaveChangesAsync(ct);
    }

    public async Task DisableMfaAsync(Guid userId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        user.DisableMfa();
        await userRepository.SaveChangesAsync(ct);
    }

    private async Task<LoginResultDto> IssueSuccessAsync(Domain.Entities.User user, CancellationToken ct)
    {
        user.RegisterSuccessfulLogin();
        await userRepository.SaveChangesAsync(ct);
        var roles = await roleRepository.GetByIdsAsync(await userRepository.GetRoleIdsForUserAsync(user.Id, ct), ct);
        var (accessToken, expiresAt) = tokenService.IssueAccessToken(user, roles);
        return new LoginResultDto(LoginOutcome.Success, accessToken, expiresAt, null);
    }
}
```

**Create file: `src/SupportCrm.Application/Security/UserManagementService.cs`**

```csharp
namespace SupportCrm.Application.Security;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class UserManagementService(
    IUserRepository userRepository, IRoleRepository roleRepository,
    PasswordHashingService passwordHasher, PasswordPolicyValidator passwordPolicyValidator, TimeProvider timeProvider)
{
    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await userRepository.GetByEmailAsync(email, ct) is not null) throw new DuplicateEmailException(email);

        var errors = passwordPolicyValidator.Validate(request.InitialPassword);
        if (errors.Count > 0) throw new SecurityValidationException(errors);

        var now = timeProvider.GetUtcNow();
        var user = new User(email, "placeholder", now); // real hash computed below, once the User instance exists
        user.SetPassword(passwordHasher.Hash(user, request.InitialPassword), now);

        await userRepository.AddAsync(user, ct);
        await userRepository.SaveChangesAsync(ct);
        await userRepository.SetUserRolesAsync(user.Id, request.RoleIds, ct);
        await userRepository.SaveChangesAsync(ct);

        return await ToDtoAsync(user, ct);
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct)
    {
        var result = new List<UserDto>();
        foreach (var user in await userRepository.GetAllAsync(ct)) result.Add(await ToDtoAsync(user, ct));
        return result;
    }

    public async Task DeactivateAsync(Guid userId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        user.Deactivate();
        await userRepository.SaveChangesAsync(ct);
    }

    public async Task ActivateAsync(Guid userId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        user.Activate();
        await userRepository.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid userId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        await userRepository.DeleteAsync(user, ct);
        await userRepository.SaveChangesAsync(ct);
    }

    public async Task SetRolesAsync(Guid userId, SetUserRolesRequest request, CancellationToken ct)
    {
        _ = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        await userRepository.SetUserRolesAsync(userId, request.RoleIds, ct);
        await userRepository.SaveChangesAsync(ct);
    }

    private async Task<UserDto> ToDtoAsync(User user, CancellationToken ct)
    {
        var roles = await roleRepository.GetByIdsAsync(await userRepository.GetRoleIdsForUserAsync(user.Id, ct), ct);
        return new UserDto(user.Id, user.Email, user.IsActive, user.MfaEnabled, user.PasswordChangedAtUtc, user.CreatedAtUtc, roles.Select(r => r.Name).ToList());
    }
}
```

### 3 — Infrastructure: EF config + seed data, repositories, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add `using Microsoft.AspNetCore.Identity;`, DbSets (`Users`, `Roles`, `UserRoles`), and in `OnModelCreating`:

```csharp
        var seedTimestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var defaultAdminUserId = new Guid("55555555-5555-5555-5555-555555555501");
        var agentRoleId = new Guid("55555555-5555-5555-5555-555555555502");
        var teamLeadRoleId = new Guid("55555555-5555-5555-5555-555555555503");
        var managerRoleId = new Guid("55555555-5555-5555-5555-555555555504");
        var adminRoleId = new Guid("55555555-5555-5555-5555-555555555505");

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.MfaSecret).HasMaxLength(64);

            // Bootstraps the system — without a seeded admin, nobody could ever log in to create
            // the first user. Dev-only credential (admin@supportcrm.local / ChangeMe123!) —
            // flagged prominently; change it immediately in any real deployment.
            entity.HasData(new
            {
                Id = defaultAdminUserId, Email = "admin@supportcrm.local",
                PasswordHash = new PasswordHasher<User>().HashPassword(null!, "ChangeMe123!"),
                IsActive = true, MfaEnabled = false, MfaSecret = (string?)null,
                PasswordChangedAtUtc = seedTimestamp, CreatedAtUtc = seedTimestamp,
                FailedLoginAttempts = 0, LockedUntilUtc = (DateTimeOffset?)null
            });
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(128);
            entity.HasIndex(r => r.Name).IsUnique();

            entity.HasData(
                new { Id = agentRoleId, Name = "Agent", IsSystemDefined = true },
                new { Id = teamLeadRoleId, Name = "Team Lead", IsSystemDefined = true },
                new { Id = managerRoleId, Name = "Manager", IsSystemDefined = true },
                new { Id = adminRoleId, Name = "Admin", IsSystemDefined = true }
            );
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(ur => ur.Id);
            entity.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();

            entity.HasData(new { Id = new Guid("55555555-5555-5555-5555-555555555601"), UserId = defaultAdminUserId, RoleId = adminRoleId });
        });
```

(Story 46 appends `Permission`/`RolePermission` configuration — including granting the seeded Admin role every permission — right after this block, in the same method.)

**Create file: `src/SupportCrm.Infrastructure/Persistence/UserRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class UserRepository(SupportCrmDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) => dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct) => await dbContext.Users.ToListAsync(ct);
    public Task AddAsync(User user, CancellationToken ct) { dbContext.Users.Add(user); return Task.CompletedTask; }

    public Task DeleteAsync(User user, CancellationToken ct)
    {
        dbContext.UserRoles.RemoveRange(dbContext.UserRoles.Where(ur => ur.UserId == user.Id));
        dbContext.Users.Remove(user);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Guid>> GetRoleIdsForUserAsync(Guid userId, CancellationToken ct) =>
        await dbContext.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync(ct);

    public Task SetUserRolesAsync(Guid userId, IReadOnlyList<Guid> roleIds, CancellationToken ct)
    {
        dbContext.UserRoles.RemoveRange(dbContext.UserRoles.Where(ur => ur.UserId == userId));
        foreach (var roleId in roleIds) dbContext.UserRoles.Add(new UserRole(userId, roleId));
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/RoleRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class RoleRepository(SupportCrmDbContext dbContext) : IRoleRepository
{
    public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct) => dbContext.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct) => await dbContext.Roles.ToListAsync(ct);
    public async Task<IReadOnlyList<Role>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct) =>
        await dbContext.Roles.Where(r => ids.Contains(r.Id)).ToListAsync(ct);
    public Task AddAsync(Role role, CancellationToken ct) { dbContext.Roles.Add(role); return Task.CompletedTask; }
    public Task DeleteAsync(Role role, CancellationToken ct) { dbContext.Roles.Remove(role); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add `using SupportCrm.Application.Security;` and, before `return services;`:

```csharp
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<PasswordHashingService>();
        services.AddScoped<PasswordPolicyValidator>();
        services.AddScoped<TotpService>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserManagementService>();
```

### 4 — Api: `Program.cs` auth wiring, `AuthController`, `UsersController`, `appsettings.json`

**File: `src/SupportCrm.Api/Program.cs`** — add near the very top (before token validation ever runs — a well-known ASP.NET Core fix, otherwise "sub"/"email"/custom claim types get silently remapped to long `ClaimTypes.*` URIs and every `FindFirst("sub")`-style lookup in this feature silently fails):

```csharp
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
```

Add JWT authentication (after `builder.Services.AddInfrastructure(...)`, before `var app = builder.Build();`):

```csharp
var jwtSection = builder.Configuration.GetSection(SupportCrm.Application.Security.JwtOptions.SectionName);
var jwtSigningKey = jwtSection["SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

builder.Services
    .AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true, ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true
        };
        // "Deactivated accounts immediately lose access" — re-checked on every authenticated
        // request, not just at login. An already-issued token for a deactivated user is
        // rejected on its very next use, not just once it naturally expires.
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sub = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
                if (!Guid.TryParse(sub, out var userId)) { context.Fail("Invalid token."); return; }
                var userRepository = context.HttpContext.RequestServices.GetRequiredService<SupportCrm.Domain.Repositories.IUserRepository>();
                var user = await userRepository.GetByIdAsync(userId, context.HttpContext.RequestAborted);
                if (user is null || !user.IsActive) context.Fail("Account is deactivated.");
            }
        };
    });
builder.Services.AddAuthorization();
```

Add to the pipeline, before `app.UseAuthorization();` (which already exists):

```csharp
app.UseAuthentication();
```

**File: `src/SupportCrm.Api/appsettings.json`** — add:

```json
  "Jwt": {
    "SigningKey": "dev-only-signing-key-change-in-any-real-deployment-3f9a7c1e5b8d4a2f",
    "Issuer": "SupportCrm",
    "Audience": "SupportCrm",
    "AccessTokenExpiryMinutes": 30,
    "MfaChallengeExpiryMinutes": 5
  },
  "Security": {
    "PasswordMinLength": 10,
    "PasswordRequireUppercase": true,
    "PasswordRequireLowercase": true,
    "PasswordRequireDigit": true,
    "PasswordRequireSpecialChar": true,
    "PasswordMaxAgeDays": 90,
    "MaxFailedLoginAttempts": 5,
    "LockoutMinutes": 15
  }
```

**Create file: `src/SupportCrm.Api/Controllers/AuthController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Security;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResultDto>> Login([FromBody] LoginRequest request, CancellationToken ct) =>
        Ok(await authService.LoginAsync(request, ct));

    [HttpPost("login/mfa")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResultDto>> CompleteMfaLogin([FromBody] CompleteMfaLoginRequest request, CancellationToken ct) =>
        Ok(await authService.CompleteMfaLoginAsync(request, ct));

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        try { await authService.ChangePasswordAsync(CurrentUserId(), request, ct); return NoContent(); }
        catch (SecurityValidationException ex) { return BadRequest(new { errors = ex.Errors }); }
    }

    [HttpPost("mfa/enable")]
    [Authorize]
    public async Task<ActionResult<MfaSetupDto>> EnableMfa(CancellationToken ct) => await authService.BeginMfaSetupAsync(CurrentUserId(), ct);

    [HttpPost("mfa/confirm")]
    [Authorize]
    public async Task<IActionResult> ConfirmMfa([FromBody] ConfirmMfaRequest request, CancellationToken ct)
    {
        try { await authService.ConfirmMfaSetupAsync(CurrentUserId(), request, ct); return NoContent(); }
        catch (SecurityValidationException ex) { return BadRequest(new { errors = ex.Errors }); }
    }

    [HttpPost("mfa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableMfa(CancellationToken ct)
    {
        await authService.DisableMfaAsync(CurrentUserId(), ct);
        return NoContent();
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ?? throw new InvalidOperationException("Missing sub claim."));
}
```

(Story 46 adds `GET /api/auth/me`, once `IPermissionChecker` exists.)

**Create file: `src/SupportCrm.Api/Controllers/UsersController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Security;

[ApiController]
[Route("api/admin/users")]
[Authorize]
public class UsersController(UserManagementService userManagementService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(CancellationToken ct) => Ok(await userManagementService.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        try { return await userManagementService.CreateAsync(request, ct); }
        catch (DuplicateEmailException ex) { return Conflict(new { error = ex.Message }); }
        catch (SecurityValidationException ex) { return BadRequest(new { errors = ex.Errors }); }
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        try { await userManagementService.DeactivateAsync(id, ct); return NoContent(); }
        catch (UserNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        try { await userManagementService.ActivateAsync(id, ct); return NoContent(); }
        catch (UserNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await userManagementService.DeleteAsync(id, ct); return NoContent(); }
        catch (UserNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> SetRoles(Guid id, [FromBody] SetUserRolesRequest request, CancellationToken ct)
    {
        try { await userManagementService.SetRolesAsync(id, request, ct); return NoContent(); }
        catch (UserNotFoundException) { return NotFound(); }
    }
}
```

(This controller is bare `[Authorize]` in this story — Story 46 adds `[RequirePermission("Administration", ...)]` per action once that attribute exists.)

---

## Edge Cases & Failure Modes

- **Login with a correct password on a deactivated account** — `AccountDeactivated`, distinct from `InvalidCredentials` — tells the caller *why*, without revealing whether the password was actually right to someone probing a deactivated account (the outcome enum reveals deactivation regardless — an accepted, minor information leak for this app's threat model, flagged not hidden).
- **5 consecutive failed logins** (default `MaxFailedLoginAttempts`) — the account locks for `LockoutMinutes`; a *correct* password during the lockout window still returns `AccountLocked`, not `Success`.
- **Password older than `PasswordMaxAgeDays`** — login is blocked with `PasswordExpired` before any token is issued; there is no partial/temporary session — the only way forward is `change-password`, which itself isn't behind the auth pipeline in a way that helps here since it requires `[Authorize]`... **flagged as a real gap**: an expired-password user currently has no path to change their password without first being unlocked by an admin. A follow-up "change password using the expired credentials directly" flow is a reasonable next step, out of scope for this story's already-large surface.
- **Deleting a user who owns no `Agent`/ticket/task rows** — always safe; `User` and `Agent` are unrelated tables (see this story's own intake note).
- **`OnTokenValidated` querying a deleted user** — `GetByIdAsync` returns `null`, treated the same as deactivated (`context.Fail`).

---

## Test Plan

1. **Unit — `tests/SupportCrm.Domain.Tests/Entities/UserTests.cs`**: `RegisterFailedLogin_AtThreshold_LocksAccount`; `RegisterSuccessfulLogin_ResetsLockout`.
2. **Unit — `tests/SupportCrm.Application.Tests/Security/TotpServiceTests.cs`**: `ValidateCode_CurrentStep_Succeeds`; `ValidateCode_TwoStepsAway_Fails`.
3. **Unit — `tests/SupportCrm.Application.Tests/Security/AuthServiceTests.cs`**: `LoginAsync_DeactivatedAccount_ReturnsAccountDeactivated`; `LoginAsync_MfaEnabled_ReturnsChallengeNotToken`.
4. **Integration — `tests/SupportCrm.Api.Tests/Controllers/AuthControllerTests.cs`**: `Post_Login_SeededAdmin_Succeeds`.

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddSecurityAndAdministration --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` (run once, after Story 46's `Permission`/`RolePermission` model additions land too — one consolidated migration for the whole feature, not one per story, per this codebase's own established EF-migration-batching lesson).
3. **Manual smoke:** `POST /api/auth/login` with `admin@supportcrm.local` / `ChangeMe123!` returns a token.

---

## Done Criteria

- [ ] Admin can create/deactivate/activate/delete users and assign roles.
- [ ] Login issues a real JWT; MFA and password-expiry paths work.
- [ ] Deactivation blocks the very next authenticated request, not just the next login.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
