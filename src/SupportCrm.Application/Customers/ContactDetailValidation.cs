namespace SupportCrm.Application.Customers;

using System.Text.RegularExpressions;
using SupportCrm.Domain.Entities;

public static class ContactDetailValidation
{
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"^\+?[0-9]{7,15}$", RegexOptions.Compiled);

    public static string? Validate(ContactChannelType channelType, string value) => channelType switch
    {
        ContactChannelType.Email => EmailPattern.IsMatch(value) ? null : "Enter a valid email address.",
        ContactChannelType.Phone or ContactChannelType.WhatsApp =>
            PhonePattern.IsMatch(value) ? null : "Enter a valid phone number (digits only, optionally starting with '+').",
        _ => "Unsupported contact channel."
    };
}
