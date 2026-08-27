namespace SupportCrm.Api.Security;

public static class RateLimitPolicies
{
    // INT-1 — applied to every api/integrations/v1/* controller via [EnableRateLimiting].
    public const string IntegrationsApi = "IntegrationsApi";
}
