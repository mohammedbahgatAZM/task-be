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
