namespace SupportCrm.Domain.Entities;

public class Team
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public Guid? DepartmentId { get; private set; }

    private Team() { } // EF Core

    public Team(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Team name is required.", nameof(name));
        Id = Guid.NewGuid();
        Name = name;
    }

    public void SetDepartment(Guid? departmentId) => DepartmentId = departmentId;
}
