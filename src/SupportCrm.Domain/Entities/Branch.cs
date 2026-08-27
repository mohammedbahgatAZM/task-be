namespace SupportCrm.Domain.Entities;

public class Branch
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string? DefaultLanguage { get; private set; } // "en" | "ar"
    public string? ContactNumber { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Branch() { }

    public Branch(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Branch name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Branch code is required.", nameof(code));
        Id = Guid.NewGuid();
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
    }

    public void UpdateSettings(string? defaultLanguage, string? contactNumber)
    {
        DefaultLanguage = defaultLanguage is "en" or "ar" ? defaultLanguage : null;
        ContactNumber = contactNumber?.Trim();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
