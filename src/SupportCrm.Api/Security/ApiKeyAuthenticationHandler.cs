namespace SupportCrm.Api.Security;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Integrations;

// INT-1 — "API access is secured via authentication tokens/API keys scoped by permission." A
// second, independent authentication scheme alongside the JWT bearer scheme the agent UI uses —
// registered under its own name ("ApiKey") so it never applies to internal controllers unless
// they explicitly opt in via [Authorize(AuthenticationSchemes = "ApiKey", Policy = "...")].
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "ApiKey";
}

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ApiKeyService apiKeyService) : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    public const string HeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValues) || string.IsNullOrWhiteSpace(headerValues.ToString()))
            return AuthenticateResult.Fail($"Missing {HeaderName} header.");

        var apiKey = await apiKeyService.ValidateAsync(headerValues.ToString(), Context.RequestAborted);
        if (apiKey is null)
            return AuthenticateResult.Fail("Invalid or revoked API key.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, apiKey.Id.ToString()),
            new("api_key_name", apiKey.Name)
        };
        claims.AddRange(apiKey.ScopeList.Select(scope => new Claim("scope", scope)));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    // INT-1 — "error responses are clearly documented and consistently applied." Every error
    // path in this API (validation, not-found, rate limiting) returns { "error": "..." }; a bare
    // framework-default 401/403 with no body would be the one inconsistent exception without this.
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        await Response.WriteAsJsonAsync(new { error = "Missing or invalid API key. Provide a valid key in the X-Api-Key header." });
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/json";
        await Response.WriteAsJsonAsync(new { error = "This API key does not have the required scope for this operation." });
    }
}
