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
