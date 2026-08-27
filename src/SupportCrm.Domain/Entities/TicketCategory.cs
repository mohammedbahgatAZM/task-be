namespace SupportCrm.Domain.Entities;

public class TicketCategory
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public Guid? ParentCategoryId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid? DepartmentId { get; private set; }

    private TicketCategory() { } // EF Core

    public TicketCategory(string name, Guid? parentCategoryId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name is required.", nameof(name));

        Id = Guid.NewGuid();
        Name = name;
        ParentCategoryId = parentCategoryId;
    }

    public void Deactivate() => IsActive = false;

    public void SetDepartment(Guid? departmentId) => DepartmentId = departmentId;
}
