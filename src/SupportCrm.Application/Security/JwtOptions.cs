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
