namespace SupportCrm.Domain.Entities;

public class Permission
{
    public Guid Id { get; private set; }
    public string Module { get; private set; } = default!;
    public string Action { get; private set; } = default!; // "View" | "Create" | "Edit" | "Delete" | "Export"

    private Permission() { }

    public Permission(string module, string action)
    {
        Id = Guid.NewGuid();
        Module = module;
        Action = action;
    }
}
