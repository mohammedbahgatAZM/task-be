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
    private const string PasswordResetChallengePurpose = "password_reset_challenge";

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

    // Issued instead of a full access token when a login is blocked by password expiry — lets the
    // caller set a new password without first needing a valid (non-expired-policy) session, which
    // would otherwise be a dead end: [Authorize] on change-password requires a token this user
    // can never legitimately obtain while their password is expired.
    public (string Token, DateTimeOffset ExpiresAtUtc) IssuePasswordResetChallengeToken(Guid userId)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(options.Value.MfaChallengeExpiryMinutes);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, userId.ToString()), new(PurposeClaimType, PasswordResetChallengePurpose) };
        return (WriteToken(claims, now, expiresAt), expiresAt);
    }

    public Guid? ValidatePasswordResetChallengeToken(string token)
    {
        var principal = ValidateAndGetPrincipal(token);
        if (principal is null || principal.FindFirst(PurposeClaimType)?.Value != PasswordResetChallengePurpose) return null;
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
