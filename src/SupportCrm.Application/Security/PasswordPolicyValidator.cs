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
