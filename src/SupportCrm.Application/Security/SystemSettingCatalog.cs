namespace SupportCrm.Application.Security;

using System.Text.Json;

public record SystemSettingDefinition(string Key, string DisplayName, string ValueType, string DefaultValue, Func<string, string?> Validate);

// A fixed, code-defined catalog — not a fully generic admin-authorable schema. Adding a fourth
// setting means adding one entry here, not a migration; this is a deliberate scope boundary,
// not a limitation anyone hit by accident.
public static class SystemSettingCatalog
{
    public static readonly IReadOnlyList<SystemSettingDefinition> Definitions = new List<SystemSettingDefinition>
    {
        new("SupportedLanguages", "Supported languages", "Json", "[\"en\",\"ar\"]", ValidateLanguages),
        new("NotifyCustomerOnStatusChangeByDefault", "Notify customer on status change by default", "Bool", "true", ValidateBool),
        new("NotifyCustomerOnResolutionByDefault", "Notify customer on resolution by default", "Bool", "true", ValidateBool)
    };

    public static SystemSettingDefinition? Find(string key) => Definitions.FirstOrDefault(d => d.Key == key);

    private static string? ValidateBool(string value) => bool.TryParse(value, out _) ? null : "Must be 'true' or 'false'.";

    private static string? ValidateLanguages(string value)
    {
        List<string>? codes;
        try { codes = JsonSerializer.Deserialize<List<string>>(value); }
        catch { return "Must be a JSON array of language codes, e.g. [\"en\",\"ar\"]."; }
        if (codes is null || codes.Count == 0) return "At least one language is required.";
        if (codes.Any(c => c.Length != 2)) return "Each language code must be 2 letters (ISO 639-1), e.g. \"en\".";
        return null;
    }
}
